using System;

namespace AudioRestoration.Core.Processing.DSP
{
    /// <summary>
    /// Сводит разделенные стемы обратно в единый стерео-файл с защитой от перегруза.
    /// </summary>
    public class AudioMixer
    {
        /// --- НАСТРОЙКИ ---
        private readonly float _targetPeak;

        /// <param name="targetPeak">Максимально допустимый уровень (например, 0.99 для запаса).</param>
        public AudioMixer(float targetPeak = 0.99f)
        {
            _targetPeak = targetPeak;
        }

        /// <summary>
        /// Mixes the provided audio stems into a single track, applying additive mixing and peak normalization to
        /// prevent clipping.
        /// </summary>
        /// <remarks>If the combined signal exceeds the target peak level, the method proportionally
        /// reduces the amplitude of all samples to prevent clipping. All input stem arrays must be of equal length for
        /// correct mixing.</remarks>
        /// <param name="stems">The set of audio stems to be mixed. Must contain non-null arrays for vocals, drums, bass, and other
        /// components, all of equal length.</param>
        /// <returns>An array of floating-point samples representing the mixed audio track. The array length matches the input
        /// stem arrays.</returns>
        /// <exception cref="ArgumentException">Thrown if any of the required stems (vocals, drums, bass, or other) are null.</exception>
        public float[] Mix(RestorationResult stems)
        {
            if (stems.Vocals == null || stems.Drums == null || stems.Bass == null || stems.Other == null)
                throw new ArgumentException("Для микширования требуются все 4 стема.");

            int length = stems.Vocals.Length;
            float[] mixed = new float[length];
            float maxPeak = 0f;

            // 1. Аддитивное микширование (сумма всех каналов)
            for (int i = 0; i < length; i++)
            {
                float sum = stems.Vocals[i] + stems.Drums[i] + stems.Bass[i] + stems.Other[i];
                mixed[i] = sum;

                // Отслеживаем абсолютный максимум для защиты от клиппинга
                float absSum = Math.Abs(sum);
                if (absSum > maxPeak)
                {
                    maxPeak = absSum;
                }
            }

            // 2. Защитная нормализация (Limiting)
            // Если сумма пробила потолок, пропорционально уменьшаем громкость всего трека
            if (maxPeak > _targetPeak)
            {
                float reductionRatio = _targetPeak / maxPeak;

                for (int i = 0; i < length; i++)
                {
                    mixed[i] *= reductionRatio;
                }
            }

            return mixed;
        }
    }
}