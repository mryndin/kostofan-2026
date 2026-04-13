using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using NAudio.Wave;
using AudioRestoration.Core.Processing;

namespace AudioRestoration.Tests
{
    /// <summary>
    /// Provides integration tests for the audio restoration pipeline, verifying end-to-end processing and file output.
    /// </summary>
    /// <remarks>This class initializes the restoration pipeline with a specified model and performs tests
    /// that load audio, process it through the pipeline, and validate the results. It ensures that the pipeline
    /// produces expected outputs and that the restored audio meets basic quality checks. The class also manages test
    /// resources and cleans up after tests are run.</remarks>
    public class PipelineIntegrationTests : IDisposable
    {
        /// <summary>
        /// Provides an output helper for capturing test output during test execution.
        /// </summary>
        /// <remarks>Typically used to write diagnostic messages or additional information to the test
        /// output stream. The output is visible in test results and can assist with debugging or analysis.</remarks>
        private readonly ITestOutputHelper _output;
        // Путь к модели ONNX, используемой в тестах. Убедитесь, что модель доступна по этому пути перед запуском тестов.
        private string _modelPath = @"..\..\..\..\AudioRestoration.ConsoleApp\model.onnx";
        // Тестовый WAV файл для обработки. Убедитесь, что этот файл существует в директории перед запуском тестов.
        private readonly string _testFile = @"..\..\..\..\test_input.wav";
        // Экземпляр Pipeline для выполнения тестов. Инициализируется в конструкторе и используется в тестовых методах.
        private readonly RestorationPipeline _pipeline;

        /// <summary>
        /// Initializes a new instance of the PipelineIntegrationTests class and prepares the restoration pipeline for
        /// testing.
        /// </summary>
        /// <remarks>This constructor verifies the presence of the model file before initializing the
        /// pipeline. Ensure that the model file is available at the expected location prior to running tests.</remarks>
        /// <param name="output">The test output helper used to capture and display test output.</param>
        /// <exception cref="FileNotFoundException">Thrown if the required model file does not exist at the specified path.</exception>
        public PipelineIntegrationTests(ITestOutputHelper output)
        {
            _output = output;

            // Проверяем наличие модели перед запуском
            if (!File.Exists(_modelPath))
                throw new FileNotFoundException($"Модель не найдена по пути: {Path.GetFullPath(_modelPath)}");

            _pipeline = new RestorationPipeline(_modelPath);
        }

        /// <summary>
        /// Verifies that the end-to-end audio restoration pipeline processes an input file, saves the separated and
        /// restored audio tracks, and produces valid output files.
        /// </summary>
        /// <remarks>This test covers the full workflow, including loading an audio file, running the
        /// pipeline for AI-based separation and DSP restoration, saving the resulting tracks, and performing basic
        /// assertions on the output. The test ensures that the restoration process completes successfully and that the
        /// output files meet expected criteria, such as length and normalization.</remarks>
        [Fact]
        public void ShouldRunEndToEndRestorationAndSaveFiles()
        {
            // 1. Загрузка
            _output.WriteLine("Загрузка аудио...");
            float[] inputAudio = LoadAudio(_testFile, out int sampleRate);
            
            _output.WriteLine($"Файл загружен. Длительность: {inputAudio.Length / (2.0 * sampleRate):F2} сек.");

            // 2. Обработка через Оркестратор
            _output.WriteLine("Запуск Pipeline (AI Separation + DSP DeClip)...");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            
            var result = _pipeline.Run(inputAudio);
            
            watch.Stop();
            _output.WriteLine($"Обработка завершена за {watch.Elapsed.TotalSeconds:F2} сек.");

            // 3. Сохранение результатов
            string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RestorationResults");
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            SaveAudio(Path.Combine(outputDir, "vocals_fixed.wav"), result.Vocals, sampleRate);
            SaveAudio(Path.Combine(outputDir, "drums_fixed.wav"), result.Drums, sampleRate);
            SaveAudio(Path.Combine(outputDir, "bass_fixed.wav"), result.Bass, sampleRate);
            SaveAudio(Path.Combine(outputDir, "other_restored.wav"), result.Other, sampleRate);

            // СОХРАНЯЕМ ФИНАЛЬНЫЙ МИКС
            SaveAudio(Path.Combine(outputDir, "master_restored.wav"), result.FinalMix, sampleRate);

            _output.WriteLine($"Результаты сохранены в: {outputDir}");

            // 4. Базовые проверки
            Assert.NotNull(result.Other);
            Assert.Equal(inputAudio.Length, result.Other.Length);
            
            // Проверка нормализации: пиковый уровень не должен превышать 1.0 (0 dB)
            float maxPeak = result.FinalMix.Max(Math.Abs);
            Assert.True(maxPeak <= 1.0f, $"Пик после реставрации слишком высокий: {maxPeak}");
        }

        // --- ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ДЛЯ РАБОТЫ С WAV ---

        /// <summary>
        /// Загружает WAV файл и преобразует его в массив float (interleaved stereo).
        /// </summary>
        private float[] LoadAudio(string path, out int sampleRate)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Тестовый файл не найден. Положите test_input.wav в output директорию.");

            using var reader = new AudioFileReader(path);
            sampleRate = reader.WaveFormat.SampleRate;
            
            if (reader.WaveFormat.Channels != 2)
                throw new NotSupportedException("Поддерживается только стерео (2 канала).");

            // Читаем все сэмплы в массив float
            float[] buffer = new float[reader.Length / (reader.WaveFormat.BitsPerSample / 8)];
            int read = reader.Read(buffer, 0, buffer.Length);
            
            return buffer.Take(read).ToArray();
        }

        /// <summary>
        /// Сохраняет массив float обратно в WAV файл.
        /// </summary>
        private void SaveAudio(string path, float[] data, int sampleRate)
        {
            // Создаем формат: IEEE Floating Point, Stereo
            var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
            using var writer = new WaveFileWriter(path, format);
            writer.WriteSamples(data, 0, data.Length);
        }

        public void Dispose()
        {
            _pipeline?.Dispose();
        }
    }
}