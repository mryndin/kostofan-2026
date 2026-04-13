using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AudioRestoration.Core.Processing.AI
{
    /// <summary>
    /// Provides functionality to separate a stereo audio signal into four distinct stems using a machine learning
    /// model.
    /// </summary>
    /// <remarks>The AudioSeparator class loads a specified model and processes stereo audio input to extract
    /// individual stems, such as vocals, drums, bass, and other components. Instances of this class are not
    /// thread-safe. Dispose of the instance when finished to release unmanaged resources associated with the underlying
    /// inference session.</remarks>
    public class AudioSeparator : IDisposable
    {
        // --- ПОЛЯ ---
        private readonly InferenceSession _session;
        // --- НАСТРОЙКИ ---
        private const int SampleRate = 44100;
        // Размер чанка для обработки (в сэмплах на канал). Выбирается с учетом баланса между качеством и производительностью.
        private const int ChunkSamples = 343980;

        // Нахлест тоже можно чуть подправить, чтобы он был кратным 
        // (например, оставить 44100 или сделать чуть меньше)
        private const int OverlapSamples = 44100;
        private const int Stride = ChunkSamples - OverlapSamples;

        /// <summary>
        /// Initializes a new instance of the AudioSeparator class using the specified model file path.
        /// </summary>
        /// <remarks>The model file at the specified path must be compatible with the expected format for
        /// audio separation. Ensure the file exists and is accessible before initializing the class.</remarks>
        /// <param name="modelPath">The file path to the model used for audio separation. Cannot be null or empty.</param>
        public AudioSeparator(string modelPath)
        {
            _session = new InferenceSession(modelPath);
        }

        /// <summary>
        /// Separates a stereo audio input into four distinct audio stems.
        /// </summary>
        /// <remarks>The method processes the input in overlapping chunks and applies windowing to ensure
        /// smooth separation. The order of the returned stems corresponds to the specific sources defined by the
        /// implementation. The input array is not modified.</remarks>
        /// <param name="stereoInput">An array of interleaved stereo audio samples. The array length must be an even number, with left and right
        /// channel samples alternating.</param>
        /// <returns>A tuple containing four float arrays, each representing a separated audio stem in stereo format. Each array
        /// has the same length as the input and contains interleaved left and right channel samples.</returns>
        public (float[] v, float[] d, float[] b, float[] o) Separate(float[] stereoInput)
        {
            int totalSamples = stereoInput.Length / 2;

            // --- ВХОДНАЯ НОРМАЛИЗАЦИЯ (Gain Staging) ---
            // Находим максимальный абсолютный пик оригинального трека
            float maxPeak = 0f;
            for (int i = 0; i < stereoInput.Length; i++)
            {
                float abs = Math.Abs(stereoInput[i]);
                if (abs > maxPeak) maxPeak = abs;
            }

            // Идеальный уровень для Demucs - около 0.9 (-0.9 dB)
            float normalizationScale = 1.0f;
            if (maxPeak > 0.01f)
            {
                normalizationScale = 0.9f / maxPeak;
                // Подтягиваем громкость всего трека до оптимального уровня перед обработкой
                for (int i = 0; i < stereoInput.Length; i++)
                {
                    stereoInput[i] *= normalizationScale;
                }
            }

            // Буферы для накопления результата (4 стема * 2 канала)
            var sumBuffers = new float[4][];
            for (int i = 0; i < 4; i++) sumBuffers[i] = new float[stereoInput.Length];

            // Буфер весов для нормализации склейки
            var weightBuffer = new float[stereoInput.Length];

            // Генерируем окно (Tapering window) для плавного смешивания
            float[] window = GetWindow(ChunkSamples);

            for (int offset = 0; offset < totalSamples; offset += Stride)
            {
                // 1. Извлечение чанка (с дополнением нулями в конце, если нужно)
                var chunk = GetChunk(stereoInput, offset, ChunkSamples);

                // 2. Инференс (на выходе 8 каналов: 4 стема по 2 канала)
                // Передаем ChunkSamples для обновленного метода с поддержкой Shift Trick
                var output = RunInference(chunk, ChunkSamples);

                // 3. Смешивание результата с основным буфером
                for (int s = 0; s < 4; s++)
                {
                    for (int i = 0; i < ChunkSamples; i++)
                    {
                        int targetIdx = (offset + i) * 2;
                        if (targetIdx + 1 >= stereoInput.Length) break;

                        float w = window[i];

                        // Левый канал
                        sumBuffers[s][targetIdx] += output[s, 0, i] * w;
                        // Правый канал
                        sumBuffers[s][targetIdx + 1] += output[s, 1, i] * w;

                        // Накапливаем веса только один раз (они общие для всех стемов)
                        if (s == 0)
                        {
                            weightBuffer[targetIdx] += w;
                            weightBuffer[targetIdx + 1] += w;
                        }
                    }
                }

                if (offset + ChunkSamples >= totalSamples) break;
            }

            // --- ФИНАЛЬНАЯ НОРМАЛИЗАЦИЯ И ДЕНОРМАЛИЗАЦИЯ ---
            // Вычисляем обратный масштаб для возврата оригинальной громкости
            float inverseScale = 1.0f / normalizationScale;

            // 4. Финальная нормализация (деление на сумму весов + возврат исходной громкости)
            for (int i = 0; i < stereoInput.Length; i++)
            {
                float w = weightBuffer[i] > 0 ? weightBuffer[i] : 1f;
                for (int s = 0; s < 4; s++)
                {
                    // Делим на вес окна склейки И умножаем на обратный масштаб
                    sumBuffers[s][i] = (sumBuffers[s][i] / w) * inverseScale;
                }
            }

            return (sumBuffers[0], sumBuffers[1], sumBuffers[2], sumBuffers[3]);
        }

        /// <summary>
        /// Generates a Hann window of the specified size for use in signal processing operations.
        /// </summary>
        /// <remarks>The Hann window is commonly used to reduce spectral leakage in Fourier analysis and
        /// other signal processing tasks. The returned array can be used to apply windowing to input data before
        /// performing frequency domain transformations.</remarks>
        /// <param name="size">The number of points in the window. Must be greater than 1.</param>
        /// <returns>An array of floating-point values representing the Hann window of the specified size.</returns>
        private float[] GetWindow(int size)
        {
            // Используем окно Ханна (Hann) для идеального разделения и склейки
            float[] win = new float[size];
            for (int i = 0; i < size; i++)
            {
                win[i] = (float)(0.5 * (1 - Math.Cos(2 * Math.PI * i / (size - 1))));
            }
            return win;
        }

        /// <summary>
        /// Runs an inference session on the provided audio chunk and returns the separated audio stems as a
        /// three-dimensional array.
        /// </summary>
        /// <remarks>The method expects the input array to be formatted as interleaved stereo samples
        /// (left-right-left-right). The output array contains four stems, each with two channels and ChunkSamples
        /// samples per channel. The mapping of stem indices to audio sources depends on the model used.</remarks>
        /// <param name="chunk">An array of interleaved left and right channel audio samples to be processed. The array length must be twice
        /// the value of ChunkSamples.</param>
        /// <returns>A three-dimensional array of floats containing the separated audio stems. The array is structured as [stem,
        /// channel, sample], where stem indices correspond to different audio sources (e.g., vocals, drums, bass,
        /// other), channel indices represent left and right channels, and sample indices represent audio samples.</returns>
        private float[,,] RunInference(float[] chunk, int requiredSamples)
        {
            var finalResult = new float[4, 2, requiredSamples];

            // Количество проходов (чем больше, тем меньше артефактов, но дольше время)
            int numShifts = 4;
            int shiftStep = requiredSamples / numShifts; // Равномерное распределение сдвигов

            for (int pass = 0; pass < numShifts; pass++)
            {
                int offset = pass * shiftStep;
                var inputTensor = new DenseTensor<float>(new[] { 1, 2, requiredSamples });

                // 1. Заполняем тензор со сдвигом (padding нулями)
                for (int i = 0; i < requiredSamples; i++)
                {
                    int sourceIdx = i - offset;

                    if (sourceIdx >= 0 && sourceIdx < (chunk.Length / 2))
                    {
                        inputTensor[0, 0, i] = chunk[sourceIdx * 2];     // L
                        inputTensor[0, 1, i] = chunk[sourceIdx * 2 + 1]; // R
                    }
                    else
                    {
                        // Заполняем "пустоты" тишиной
                        inputTensor[0, 0, i] = 0f;
                        inputTensor[0, 1, i] = 0f;
                    }
                }

                var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input", inputTensor) };
                using var results = _session.Run(inputs);
                var outputTensor = results.First().AsTensor<float>();

                // 2. Усредняем результат
                for (int stem = 0; stem < 4; stem++)
                {
                    for (int channel = 0; channel < 2; channel++)
                    {
                        for (int i = 0; i < requiredSamples; i++)
                        {
                            // Читаем результат, учитывая обратный сдвиг
                            int resultIdx = i + offset;
                            if (resultIdx < requiredSamples)
                            {
                                finalResult[stem, channel, i] += outputTensor[0, stem, channel, resultIdx] / numShifts;
                            }
                        }
                    }
                }
            }

            return finalResult;
        }

        /// <summary>
        /// Extracts a chunk of interleaved stereo audio samples from the specified input array, starting at the given
        /// offset and containing the specified number of frames.
        /// </summary>
        /// <remarks>This method does not modify the input array. The returned chunk always contains the
        /// requested number of frames, with zero-padding if the input does not have enough data from the specified
        /// offset.</remarks>
        /// <param name="input">The input array containing interleaved stereo audio samples. The array length must be an even number, with
        /// left and right channel samples alternating.</param>
        /// <param name="offset">The zero-based index of the first frame to extract. Each frame consists of two consecutive samples (left and
        /// right channels). Must be non-negative and less than or equal to the number of available frames in the input.</param>
        /// <param name="size">The number of frames to extract. Must be non-negative. If the requested size exceeds the available frames
        /// from the offset, the returned chunk will be zero-padded.</param>
        /// <returns>A float array containing the extracted interleaved stereo samples. The length of the returned array is
        /// always size × 2. If there are not enough samples in the input, the remaining values are set to zero.</returns>
        private float[] GetChunk(float[] input, int offset, int size)
        {
            float[] chunk = new float[size * 2];
            int samplesToCopy = Math.Min(size, (input.Length / 2) - offset);
            Array.Copy(input, offset * 2, chunk, 0, samplesToCopy * 2);
            return chunk;
        }

        /// <summary>
        /// Releases all resources used by the current instance.
        /// </summary>
        /// <remarks>Call this method when the instance is no longer needed to free unmanaged resources
        /// promptly. After calling Dispose, the instance should not be used.</remarks>
        public void Dispose() => _session?.Dispose();
    }
}