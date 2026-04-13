using Xunit;
using AudioRestoration.Core.Processing.AI;
using NAudio.Wave;
using System.IO;

namespace AudioRestoration.Tests
{
    // Тесты для проверки корректности разделения аудио на стемы и сохранения результатов в WAV файлы.
    public class SeparationTests
    {
        // Этот тест проверяет, что аудио может быть успешно разделено на стемы и сохранено в виде отдельных WAV файлов.
        [Fact]
        public void ShouldSeparateAudioAndSaveWavs()
        {
            // Пути (подставьте свои для теста)
            string modelPath = @"..\..\..\..\AudioRestoration.ConsoleApp\model.onnx";
            string testAudio = @"test_input.wav"; // Подготовьте короткий wav

            using var separator = new AudioSeparator(modelPath);
            
            // 1. Читаем аудио через NAudio
            using var reader = new AudioFileReader(testAudio);
            float[] buffer = new float[reader.Length / 4];
            reader.Read(buffer, 0, buffer.Length);

            // 2. Разделяем
            var (vocals, drums, bass, other) = separator.Separate(buffer);

            // 3. Сохраняем результат для прослушивания
            SaveWav("drums.wav", drums);
            SaveWav("vocals.wav", vocals);
            SaveWav("other_guitars.wav", other);
            
            Assert.True(File.Exists("vocals.wav"));
        }

        // Вспомогательный метод для сохранения массива сэмплов в WAV файл с помощью NAudio.
        private void SaveWav(string path, float[] buffer)
        {
            using var writer = new WaveFileWriter(path, new WaveFormat(44100, 2));
            writer.WriteSamples(buffer, 0, buffer.Length);
        }
    }
}