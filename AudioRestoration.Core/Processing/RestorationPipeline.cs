using System;
using System.Collections.Generic;
using AudioRestoration.Core.Processing.AI;
using AudioRestoration.Core.Processing.DSP;

namespace AudioRestoration.Core.Processing
{
    /// <summary>
    /// Содержит результаты работы конвейера реставрации.
    /// </summary>
    public class RestorationResult
    {
        // Разделенные и восстановленные стемы
        public float[] Vocals { get; set; }
        public float[] Drums { get; set; }
        public float[] Bass { get; set; }
        public float[] Other { get; set; }

        // Финальный микс после восстановления и нормализации всех стемов
        public float[] FinalMix { get; set; }
    }

    /// <summary>
    /// Управляет последовательностью обработки: Разделение -> Реставрация -> Нормализация.
    /// </summary>
    public class RestorationPipeline : IDisposable
    {
        // --- ПОЛЯ ---
        private readonly AudioSeparator _separator;
        private readonly DeClipper _deClipper;
        private readonly PeakNormalizer _normalizer;
        private readonly AudioMixer _mixer;
        // Инициализация процессоров
        private readonly StemProcessor _vocalProc = new(44100, 100.0f);
        private readonly StemProcessor _drumProc = new(44100, 30.0f);
        private readonly StemProcessor _bassProc = new(44100, 40.0f);
        private readonly StemProcessor _otherProc = new(44100, 80.0f);

        // Гейты для разных стемов
    private readonly NoiseGate _vocalGate;
    private readonly NoiseGate _drumGate;
    private readonly NoiseGate _bassGate;
    private readonly NoiseGate _otherGate;

        /// <summary>
        /// Initializes a new instance of the RestorationPipeline class using the specified model path for audio
        /// processing components.
        /// </summary>
        /// <remarks>The provided model path is used to configure internal audio processing components.
        /// Ensure the path points to a valid and accessible model file.</remarks>
        /// <param name="modelPath">The file system path to the model used for audio separation. Cannot be null or empty.</param>
        public RestorationPipeline(string modelPath, float sampleRate = 44100f)
        {
            _separator = new AudioSeparator(modelPath);
            _deClipper = new DeClipper(threshold: 0.98f, minClippedLength: 3);
            _normalizer = new PeakNormalizer(targetPeakDb: -0.1f);
            _mixer = new AudioMixer(targetPeak: 0.99f);

            // Вокал: строгий порог, длинный хвост (чтобы не убить реверб)
            _vocalGate = new NoiseGate(thresholdDb: -42f, attackMs: 5f, releaseMs: 250f, sampleRate);

            // Барабаны: средний порог, короткий хвост (для четкости)
            _drumGate = new NoiseGate(thresholdDb: -45f, attackMs: 2f, releaseMs: 100f, sampleRate);

            // Бас и Остальное: мягкий порог
            _bassGate = new NoiseGate(thresholdDb: -48f, attackMs: 10f, releaseMs: 150f, sampleRate);
            _otherGate = new NoiseGate(thresholdDb: -48f, attackMs: 10f, releaseMs: 150f, sampleRate);


        }

        /// <summary>
        /// Processes a stereo audio input to separate, restore, normalize, and mix its components, returning the result
        /// as a set of audio stems and a final mix.
        /// </summary>
        /// <remarks>The method performs multiple processing steps, including source separation,
        /// restoration of clipped audio, normalization of each stem, and final mixing. The input array must be properly
        /// formatted as interleaved stereo samples for correct processing.</remarks>
        /// <param name="stereoInput">An array of floating-point values representing the stereo audio input to be processed. The array should
        /// contain interleaved left and right channel samples.</param>
        /// <returns>A RestorationResult containing the separated and restored audio stems (vocals, drums, bass, and other) as
        /// well as the final mixed output.</returns>
        public RestorationResult Run(float[] stereoInput)
        {
            // 1. Разделение (AI)
            var (v, d, b, o) = _separator.Separate(stereoInput);

            // 2. DSP-обработка (HPF -> Gate -> Gain)
            // --- ВОКАЛ ---
            _vocalProc.ProcessStem(v); // HPF
            _vocalGate.Process(v);     // Gate
            ApplyGlobalGain(v);        // Глобальная компенсация RMS

            // --- БАРАБАНЫ ---
            _drumProc.ProcessStem(d);
            _drumGate.Process(d);
            ApplyGlobalGain(d);

            // --- БАС ---
            _bassProc.ProcessStem(b);
            _bassGate.Process(b);
            ApplyGlobalGain(b);

            // --- ОСТАЛЬНОЕ (Other) ---
            _otherProc.ProcessStem(o);
            _otherGate.Process(o);
            ApplyGlobalGain(o);

            // 3. Реставрация (DeClip) 
            float[] restoredOther = _deClipper.Process(o);

            // 4. Микширование
            var result = new RestorationResult
            {
                Vocals = v,
                Drums = d,
                Bass = b,
                Other = restoredOther
            };

            // --- ФИНАЛЬНЫЙ ЭТАП ---
            // Смешиваем стемы
            float[] finalMix = _mixer.Mix(result);

            // Применяем лимитер, чтобы гарантировать пик <= 1.0 (прохождение теста)
            var limiter = new BrickwallLimiter(0.98f);
            limiter.Process(finalMix);

            result.FinalMix = finalMix;

            return result;
        }

        // Вспомогательный метод для глобальной нормализации (RMS)
        private void ApplyGlobalGain(float[] stem)
        {
            // Приводим стем к стандартному уровню RMS (-18dB)
            float gain = GainEstimator.GetNormalizationGain(stem, -18.0f);
            GainEstimator.ApplyGain(stem, gain);
        }

        /// <summary>
        /// Releases all resources used by the current instance.
        /// </summary>
        /// <remarks>Call this method when the instance is no longer needed to free associated resources
        /// promptly. After calling this method, the instance should not be used.</remarks>
        public void Dispose()
        {
            _separator?.Dispose();
        }
    }
}