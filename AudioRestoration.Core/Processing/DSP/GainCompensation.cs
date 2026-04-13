using System;
using System.Linq;

namespace AudioRestoration.Core.Processing.DSP
{
    public class GainCompensation
    {
        private readonly float _targetRms;

        public GainCompensation(float targetDb = -18.0f)
        {
            // Перевод дБ в линейный уровень
            _targetRms = (float)Math.Pow(10, targetDb / 20.0);
        }

        public void Apply(float[] samples)
        {
            // 1. Вычисляем текущий RMS всего массива
            float sumSquares = 0f;
            int count = samples.Length;

            for (int i = 0; i < count; i++)
            {
                sumSquares += samples[i] * samples[i];
            }
            
            float currentRms = (float)Math.Sqrt(sumSquares / count);

            // 2. Рассчитываем коэффициент усиления
            // Предохранитель от деления на ноль для полной тишины
            if (currentRms < 1e-6f) return;
            
            float gain = _targetRms / currentRms;

            // 3. Применяем коэффициент (усиление или ослабление)
            for (int i = 0; i < count; i++)
            {
                samples[i] *= gain;
            }
        }
    }
}