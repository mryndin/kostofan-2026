using System;

namespace AudioRestoration.Core.Processing.DSP
{
    /// <summary>
    /// Noise Gate для очистки пауз от низкоуровневого шума и артефактов нейросети.
    /// </summary>
    public class NoiseGate
    {
        private readonly float _threshold;    // Порог срабатывания (линейный)
        private readonly float _releaseRate;  // Скорость закрытия
        private readonly float _attackRate;   // Скорость открытия
        private float _currentGain = 0.0f;    // Текущий коэффициент усиления

        /// <summary>
        /// Создает экземпляр Noise Gate.
        /// </summary>
        /// <param name="thresholdDb">Порог в децибелах (напр., -45.0f). Чем ниже, тем чувствительнее.</param>
        /// <param name="attackMs">Время открытия в мс (напр., 5.0f). Защищает от щелчков.</param>
        /// <param name="releaseMs">Время закрытия в мс (напр., 200.0f). Хвосты затухания.</param>
        /// <param name="sampleRate">Частота дискретизации (напр., 44100).</param>
        public NoiseGate(float thresholdDb, float attackMs, float releaseMs, float sampleRate)
        {
            // Перевод дБ в линейное значение: 10^(db/20)
            _threshold = (float)Math.Pow(10, thresholdDb / 20.0);
            
            // Рассчитываем шаг изменения Gain на один сэмпл
            _attackRate = 1.0f / (attackMs * sampleRate / 1000.0f);
            _releaseRate = 1.0f / (releaseMs * sampleRate / 1000.0f);
        }

        /// <summary>
        /// Обрабатывает стерео-массив (L-R интерливинг).
        /// </summary>
        public void Process(float[] samples)
        {
            for (int i = 0; i < samples.Length; i += 2)
            {
                float left = samples[i];
                float right = samples[i + 1];

                // Находим пиковую амплитуду текущего сэмпла (быстрее чем RMS для гейта)
                float currentLevel = Math.Max(Math.Abs(left), Math.Abs(right));

                // Логика гейта
                if (currentLevel > _threshold)
                {
                    // Сигнал выше порога -> Открываем гейт
                    _currentGain = Math.Min(_currentGain + _attackRate, 1.0f);
                }
                else
                {
                    // Сигнал ниже порога -> Закрываем гейт
                    _currentGain = Math.Max(_currentGain - _releaseRate, 0.0f);
                }

                // Применяем подавление
                samples[i] *= _currentGain;
                samples[i + 1] *= _currentGain;
            }
        }

        /// <summary>
        /// Сброс состояния (при новом файле).
        /// </summary>
        public void Reset()
        {
            _currentGain = 0.0f;
        }
    }
}