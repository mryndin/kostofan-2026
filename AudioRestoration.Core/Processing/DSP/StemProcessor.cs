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
        private readonly FilterChain _leftChain = new();
        private readonly FilterChain _rightChain = new();

        /// <summary>
        /// Инициализация процессора стема.
        /// </summary>
        /// <param name="sampleRate">Частота дискретизации (напр. 44100)</param>
        /// <param name="cutoff">Частота среза для HPF</param>
        public StemProcessor(float sampleRate, float cutoff)
        {
            // Создаем два идентичных фильтра для каскада (даст 24 дБ/окт)
            _leftChain.AddFilter(new BiquadFilter(sampleRate, cutoff));
            _leftChain.AddFilter(new BiquadFilter(sampleRate, cutoff));

            _rightChain.AddFilter(new BiquadFilter(sampleRate, cutoff));
            _rightChain.AddFilter(new BiquadFilter(sampleRate, cutoff));
        }

        /// <summary>
        /// Применяет фильтрацию к массиву сэмплов стема (интерливинг L-R).
        /// </summary>
        /// <param name="stemSamples">Массив [L, R, L, R, ...]</param>
        public void ProcessStem(float[] samples)
        {
            for (int i = 0; i < samples.Length; i += 2)
            {
                samples[i] = _leftChain.Process(samples[i]);
                if (i + 1 < samples.Length)
                    samples[i + 1] = _rightChain.Process(samples[i + 1]);
            }
        }

        /// <summary>
        /// Сбрасывает состояние фильтров (вызывать при начале обработки нового файла).
        /// </summary>
        public void Reset()
        {
            _leftChain.Reset();
            _rightChain.Reset();
        }
    }
}