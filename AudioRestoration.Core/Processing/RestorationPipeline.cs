using System;
using System.Collections.Generic;
using AudioRestoration.Core.Processing.AI;
using AudioRestoration.Core.Processing.DSP;

namespace AudioRestoration.Core.Processing
{
    /// <summary>
    /// Содержит результаты работы конвейера реставрации.
    /// </summary>
    public class RestorationResult
    {
        // Разделенные и восстановленные стемы
        public float[] Vocals { get; set; }
        public float[] Drums { get; set; }
        public float[] Bass { get; set; }
        public float[] Other { get; set; }

        // Финальный микс после восстановления и нормализации всех стемов
        public float[] FinalMix { get; set; }
    }

    /// <summary>
    /// Управляет последовательностью обработки: Разделение -> Реставрация -> Нормализация.
    /// </summary>
    public class RestorationPipeline : IDisposable
    {
        // --- ПОЛЯ ---
        private readonly AudioSeparator _separator;
        private readonly DeClipper _deClipper;
        private readonly PeakNormalizer _normalizer;
        private readonly AudioMixer _mixer;

        /// <summary>
        /// Initializes a new instance of the RestorationPipeline class using the specified model path for audio
        /// processing components.
        /// </summary>
        /// <remarks>The provided model path is used to configure internal audio processing components.
        /// Ensure the path points to a valid and accessible model file.</remarks>
        /// <param name="modelPath">The file system path to the model used for audio separation. Cannot be null or empty.</param>
        public RestorationPipeline(string modelPath)
        {
            _separator = new AudioSeparator(modelPath);
            _deClipper = new DeClipper(threshold: 0.98f, minClippedLength: 3);
            _normalizer = new PeakNormalizer(targetPeakDb: -0.1f);
            _mixer = new AudioMixer(targetPeak: 0.99f); // ДОБАВЛЕНО
        }

        /// <summary>
        /// Processes a stereo audio input to separate, restore, normalize, and mix its components, returning the result
        /// as a set of audio stems and a final mix.
        /// </summary>
        /// <remarks>The method performs multiple processing steps, including source separation,
        /// restoration of clipped audio, normalization of each stem, and final mixing. The input array must be properly
        /// formatted as interleaved stereo samples for correct processing.</remarks>
        /// <param name="stereoInput">An array of floating-point values representing the stereo audio input to be processed. The array should
        /// contain interleaved left and right channel samples.</param>
        /// <returns>A RestorationResult containing the separated and restored audio stems (vocals, drums, bass, and other) as
        /// well as the final mixed output.</returns>
        public RestorationResult Run(float[] stereoInput)
        {
            // 1. Разделение
            var (v, d, b, o) = _separator.Separate(stereoInput);

            // 2. Реставрация
            float[] restoredOther = _deClipper.Process(o);

            // 3. Нормализация стемов
            _normalizer.Process(v);
            _normalizer.Process(d);
            _normalizer.Process(b);
            _normalizer.Process(restoredOther);

            // Собираем промежуточный результат
            var result = new RestorationResult
            {
                Vocals = v,
                Drums = d,
                Bass = b,
                Other = restoredOther
            };

            // 4. Финальное микширование (ДОБАВЛЕНО)
            result.FinalMix = _mixer.Mix(result);

            return result;
        }

        /// <summary>
        /// Releases all resources used by the current instance.
        /// </summary>
        /// <remarks>Call this method when the instance is no longer needed to free associated resources
        /// promptly. After calling this method, the instance should not be used.</remarks>
        public void Dispose()
        {
            _separator?.Dispose();
        }
    }
}