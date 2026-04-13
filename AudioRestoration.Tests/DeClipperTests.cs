using Xunit;
using Xunit.Abstractions;
using AudioRestoration.Core.Processing.DSP;
using System;
using System.Linq;

namespace AudioRestoration.Tests
{
    public class DeClipperTests
    {
        private readonly ITestOutputHelper _output;

        public DeClipperTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ShouldDetectAndRestoreClippedSineWave()
        {
            // 1. Подготовка: генерируем синусоиду (1000 Гц, 44100 Гц sample rate)
            int sampleRate = 44100;
            double frequency = 1000;
            int length = 4410; // 0.1 секунды
            float[] cleanSignal = new float[length * 2]; // Стерео
            float threshold = 0.95f;

            for (int i = 0; i < length; i++)
            {
                float sample = (float)Math.Sin(2 * Math.PI * frequency * i / sampleRate);
                // Амплитуда 1.2f, чтобы гарантированно выйти за порог 0.95f
                sample *= 1.2f;

                cleanSignal[i * 2] = sample;
                cleanSignal[i * 2 + 1] = sample;
            }

            // 2. Искусственно создаем клиппинг
            float[] clippedSignal = new float[cleanSignal.Length];
            Array.Copy(cleanSignal, clippedSignal, cleanSignal.Length);

            int clippedCount = 0;
            for (int i = 0; i < clippedSignal.Length; i++)
            {
                if (clippedSignal[i] > threshold)
                {
                    clippedSignal[i] = threshold;
                    clippedCount++;
                }
                else if (clippedSignal[i] < -threshold)
                {
                    clippedSignal[i] = -threshold;
                    clippedCount++;
                }
            }

            Assert.True(clippedCount > 0, "Клиппинг не был создан, тест не имеет смысла.");

            // 3. Запуск DeClipper
            var deClipper = new DeClipper(threshold: threshold, minClippedLength: 3);
            float[] restoredSignal = deClipper.Process(clippedSignal);

            // 4. Проверки

            // Находим индекс, где точно был срез
            int testIdx = -1;
            for (int i = 0; i < clippedSignal.Length; i++)
            {
                if (Math.Abs(clippedSignal[i]) >= threshold && Math.Abs(cleanSignal[i]) > threshold)
                {
                    testIdx = i;
                    break;
                }
            }

            Assert.True(testIdx != -1, "Не найден индекс для проверки восстановления.");

            // Проверка 1: Значение должно стать больше порога (восстановление пика)
            Assert.True(Math.Abs(restoredSignal[testIdx]) > threshold,
                $"Деклиппер не поднял пик выше порога. Было: {clippedSignal[testIdx]}, Стало: {restoredSignal[testIdx]}");

            // Проверка 2: Восстановленное значение должно быть ближе к оригиналу, чем срезанное
            double originalDiff = Math.Abs(cleanSignal[testIdx] - clippedSignal[testIdx]);
            double restoredDiff = Math.Abs(cleanSignal[testIdx] - restoredSignal[testIdx]);

            Assert.True(restoredDiff < originalDiff,
                "Восстановленное значение математически дальше от идеальной синусоиды, чем битое.");

            _output.WriteLine($"Original: {cleanSignal[testIdx]:F4}");
            _output.WriteLine($"Clipped:  {clippedSignal[testIdx]:F4}");
            _output.WriteLine($"Restored: {restoredSignal[testIdx]:F4}");
        }
    }
}