using System;

namespace AudioRestoration.Core.Processing.DSP
{
    public class BrickwallLimiter
    {
        private readonly float _threshold;

        public BrickwallLimiter(float threshold = 0.95f)
        {
            _threshold = threshold;
        }

        public void Process(float[] samples)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                float abs = Math.Abs(samples[i]);
                if (abs > _threshold)
                {
                    // Soft clipping: используем арктангенс для плавного сжатия
                    float sign = Math.Sign(samples[i]);
                    samples[i] = sign * (_threshold + (1.0f - _threshold) * (float)Math.Tanh((abs - _threshold) / (1.0f - _threshold)));
                }
            }
        }
    }
}