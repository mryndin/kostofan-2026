using System;

namespace AudioRestoration.Core.Processing.DSP
{
    /// <summary>
    /// Biquad-фильтр второго порядка.
    /// Реализует высокочастотный фильтр (HPF) с использованием топологии Direct Form 1.
    /// </summary>
    public class BiquadFilter : ISoundFilter
    {
        // Коэффициенты фильтра
        private float _b0, _b1, _b2, _a1, _a2;
        
        // История (состояние) для предотвращения щелчков на стыках
        private float _x1, _x2, _y1, _y2;

        /// <summary>
        /// Конструктор фильтра.
        /// </summary>
        /// <param name="sampleRate">Частота дискретизации (напр. 44100)</param>
        /// <param name="cutoffFrequency">Частота среза (напр. 100.0)</param>
        public BiquadFilter(float sampleRate, float cutoffFrequency)
        {
            float omega = (float)(2.0 * Math.PI * cutoffFrequency / sampleRate);
            float sn = (float)Math.Sin(omega);
            float cs = (float)Math.Cos(omega);
            
            // Q = 0.707 (Баттерворт - максимально плоская АЧХ в полосе пропускания)
            float alpha = sn / (2.0f * 0.7071f); 

            // Коэффициенты для HPF
            float b0 = (1 + cs) / 2.0f;
            float b1 = -(1 + cs);
            float b2 = (1 + cs) / 2.0f;
            float a0 = 1 + alpha;
            float a1 = -2 * cs;
            float a2 = 1 - alpha;

            // Нормализация коэффициентов
            _b0 = b0 / a0;
            _b1 = b1 / a0;
            _b2 = b2 / a0;
            _a1 = a1 / a0;
            _a2 = a2 / a0;
        }

        /// <summary>
        /// Применяет фильтр к одному сэмплу.
        /// </summary>
        public float Process(float input)
        {
            // Формула разностного уравнения Direct Form 1
            float output = (_b0 * input) + (_b1 * _x1) + (_b2 * _x2) - (_a1 * _y1) - (_a2 * _y2);

            // Сдвиг состояния истории
            _x2 = _x1;
            _x1 = input;
            _y2 = _y1;
            _y1 = output;

            return output;
        }

        /// <summary>
        /// Сбрасывает историю фильтра (полезно при переключении треков или смене настроек).
        /// </summary>
        public void Reset()
        {
            _x1 = _x2 = _y1 = _y2 = 0f;
        }
    }
}