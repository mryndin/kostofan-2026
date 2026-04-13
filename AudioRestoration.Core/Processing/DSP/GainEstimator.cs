using System;

namespace AudioRestoration.Core.Processing.DSP
{
    /// <summary>
    /// Анализатор для глобальной нормализации громкости файла.
    /// Позволяет привести весь стем к заданному уровню RMS.
    /// </summary>
    public static class GainEstimator
    {
        /// <summary>
        /// Рассчитывает коэффициент усиления для приведения RMS сигнала к целевому уровню.
        /// </summary>
        /// <param name="samples">Массив сэмплов (стерео интерливинг L-R)</param>
        /// <param name="targetDb">Целевой уровень в дБ (стандарт для DAW: -18.0f)</param>
        /// <returns>Коэффициент, на который нужно умножить каждый сэмпл</returns>
        public static float GetNormalizationGain(float[] samples, float targetDb = -18.0f)
        {
            if (samples == null || samples.Length == 0) return 1.0f;

            // 1. Целевой уровень RMS в линейном представлении
            float targetRms = (float)Math.Pow(10, targetDb / 20.0);

            // 2. Вычисление текущего RMS всего сигнала
            double sumSquares = 0.0;
            for (int i = 0; i < samples.Length; i++)
            {
                sumSquares += (double)samples[i] * samples[i];
            }

            float currentRms = (float)Math.Sqrt(sumSquares / samples.Length);

            // 3. Защита от деления на ноль (для абсолютно тихих файлов)
            if (currentRms < 1e-7f) return 1.0f;

            // 4. Возвращаем коэффициент
            return targetRms / currentRms;
        }

        /// <summary>
        /// Метод для мгновенного применения рассчитанного gain ко всему массиву.
        /// </summary>
        public static void ApplyGain(float[] samples, float gain)
        {
            // Если gain = 1.0, ничего не делаем
            if (Math.Abs(gain - 1.0f) < 0.0001f) return;

            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= gain;
            }
        }
    }
}