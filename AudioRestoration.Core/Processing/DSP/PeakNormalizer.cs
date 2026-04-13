using System;
using System.Linq;

namespace AudioRestoration.Core.Processing.DSP
{
    /// <summary>
    /// Provides functionality to normalize the peak amplitude of audio data to a specified target level in decibels.
    /// </summary>
    /// <remarks>Use this class to adjust the peak level of an audio signal so that its highest absolute
    /// sample matches a desired decibel value. This is commonly used in audio processing to ensure consistent loudness
    /// across different audio tracks. The normalization is performed in-place on the provided audio buffer.</remarks>
    public class PeakNormalizer
    {
        // --- НАСТРОЙКИ ---
        private readonly float _targetPeakDb;

        /// <param name="targetPeakDb">Целевой пиковый уровень в децибелах (обычно -0.1 или -1.0)</param>
        public PeakNormalizer(float targetPeakDb = -0.1f)
        {
            _targetPeakDb = targetPeakDb;
        }

        /// <summary>
        /// Normalizes the amplitude of the specified audio buffer so that its peak matches the configured target peak
        /// level in decibels.
        /// </summary>
        /// <remarks>This method adjusts the gain of the input buffer in place, scaling all samples so
        /// that the highest absolute value matches the target peak. If the buffer is null, empty, or contains only zero
        /// values, no processing is performed. The operation modifies the input array directly.</remarks>
        /// <param name="audio">The array of audio samples to process. Each element represents a single sample. Cannot be null or empty.</param>
        public void Process(float[] audio)
        {
            if (audio == null || audio.Length == 0) return;

            // 1. Находим текущий абсолютный максимум (пик)
            float max = 0;
            for (int i = 0; i < audio.Length; i++)
            {
                float abs = Math.Abs(audio[i]);
                if (abs > max) max = abs;
            }

            if (max <= 0) return;

            // 2. Вычисляем целевую амплитуду из dB
            // Формула: amplitude = 10^(db/20)
            float targetAmplitude = (float)Math.Pow(10, _targetPeakDb / 20.0);

            // 3. Вычисляем коэффициент усиления (Gain)
            float gain = targetAmplitude / max;

            // 4. Применяем Gain ко всем сэмплам
            // Если пик был 1.0144, а цель 0.99, gain будет ~0.975
            for (int i = 0; i < audio.Length; i++)
            {
                audio[i] *= gain;
            }
        }
    }
}