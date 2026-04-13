using System;
using AudioRestoration.Core.Processing.DSP;

namespace AudioRestoration.Core.Processing.DSP
{
    /// <summary>
    /// Процессор для обработки одного стема (стерео).
    /// Содержит независимые фильтры для левого и правого каналов.
    /// </summary>
    public class StemProcessor
    {
        private readonly BiquadFilter _leftFilter;
        private readonly BiquadFilter _rightFilter;

        /// <summary>
        /// Инициализация процессора стема.
        /// </summary>
        /// <param name="sampleRate">Частота дискретизации (напр. 44100)</param>
        /// <param name="cutoff">Частота среза для HPF</param>
        public StemProcessor(float sampleRate, float cutoff)
        {
            _leftFilter = new BiquadFilter(sampleRate, cutoff);
            _rightFilter = new BiquadFilter(sampleRate, cutoff);
        }

        /// <summary>
        /// Применяет фильтрацию к массиву сэмплов стема (интерливинг L-R).
        /// </summary>
        /// <param name="stemSamples">Массив [L, R, L, R, ...]</param>
        public void ProcessStem(float[] stemSamples)
        {
            // Проход по интерливированному массиву с шагом 2
            for (int i = 0; i < stemSamples.Length; i += 2)
            {
                // Левый канал
                stemSamples[i] = _leftFilter.Process(stemSamples[i]);
                
                // Проверка на случай нечетного количества сэмплов (защита от выхода за массив)
                if (i + 1 < stemSamples.Length)
                {
                    // Правый канал
                    stemSamples[i + 1] = _rightFilter.Process(stemSamples[i + 1]);
                }
            }
        }

        /// <summary>
        /// Сбрасывает состояние фильтров (вызывать при начале обработки нового файла).
        /// </summary>
        public void Reset()
        {
            _leftFilter.Reset();
            _rightFilter.Reset();
        }
    }
}