using System;
using System.Collections.Generic;
using System.Linq;

namespace AudioRestoration.Core.Processing.DSP
{
    /// <summary>
    /// Represents a segment defined by a start and end index for clipping operations.
    /// </summary>
    /// <remarks>The segment is inclusive of both the start and end indices. This structure is typically used
    /// to specify a range within a collection or sequence for processing or extraction.</remarks>
    public struct ClippingSegment
    {
        public int Start;
        public int End;
        public int Length => End - Start + 1;
    }

    /// <summary>
    /// Данные для построения сплайна вокруг конкретного поврежденного участка
    /// </summary>
    internal struct InterpolationContext
    {
        public double[] X;        // Индексы (время)
        public double[] Y;        // Значения амплитуды
        public ClippingSegment Segment;
        public int SidePointsActual; // Сколько реально точек удалось взять слева
    }

    /// <summary>
    /// Provides functionality to detect and restore clipped segments in stereo audio data using Hermite cubic
    /// interpolation.
    /// </summary>
    /// <remarks>The DeClipper class processes interleaved stereo audio samples, identifies regions where the
    /// signal has been clipped, and attempts to reconstruct the original waveform within those regions. It is designed
    /// for use with floating-point audio data, where each channel is interleaved (left-right-left-right, etc.). The
    /// class is not thread-safe.</remarks>
    public class DeClipper
    {
        // --- НАСТРОЙКИ ---
        private readonly float _threshold;
        // Минимальная длина клиппированного участка для обработки (в сэмплах)
        private readonly int _minClippedLength;
        // Количество точек, которые мы будем использовать с каждой стороны от клиппированного участка для интерполяции
        private const int SidePoints = 4; // Количество точек опоры с каждой стороны

        // --- КОНСТРУКТОР ---
        public DeClipper(float threshold = 0.98f, int minClippedLength = 3)
        {
            _threshold = threshold;
            _minClippedLength = minClippedLength;
        }

        /// <summary>
        /// Processes a stereo audio signal to detect and restore clipped segments in each channel.
        /// </summary>
        /// <remarks>This method analyzes each channel of the stereo input separately to detect and
        /// interpolate clipped regions, aiming to restore audio quality. The input array is not modified. The output
        /// preserves the original interleaved channel order.</remarks>
        /// <param name="input">An array of floating-point values representing interleaved stereo audio samples. The array must have an even
        /// length, with left and right channel samples alternating.</param>
        /// <returns>A new array of floating-point values containing the processed audio samples with restored channels. The
        /// returned array has the same length and interleaving as the input.</returns>
        public float[] Process(float[] input)
        {
            float[] output = new float[input.Length];
            Array.Copy(input, output, input.Length);

            // Обрабатываем левый и правый каналы отдельно
            for (int channel = 0; channel < 2; channel++)
            {
                // Извлекаем канал (простой stride 2)
                float[] channelData = ExtractChannel(input, channel);

                var segments = DetectClipping(channelData);
                float[] restoredChannel = new float[channelData.Length];
                Array.Copy(channelData, restoredChannel, channelData.Length);

                foreach (var segment in segments)
                {
                    var ctx = PrepareContext(channelData, segment);
                    if (ctx.X.Length >= SidePoints * 2)
                    {
                        Interpolate(restoredChannel, ctx);
                    }
                }

                // Записываем исправленный канал обратно в общий массив
                for (int i = 0; i < restoredChannel.Length; i++)
                {
                    output[i * 2 + channel] = restoredChannel[i];
                }
            }

            return output;
        }

        /// Извлекает данные одного канала из интерливированного стерео массива
        private float[] ExtractChannel(float[] interleaved, int channel)
        {
            float[] data = new float[interleaved.Length / 2];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = interleaved[i * 2 + channel];
            }
            return data;
        }

        /// Находит все сегменты клиппинга в канале
        public List<ClippingSegment> DetectClipping(float[] channelData)
        {
            var segments = new List<ClippingSegment>();
            int n = channelData.Length;
            int i = 0;

            while (i < n)
            {
                if (Math.Abs(channelData[i]) >= _threshold)
                {
                    int start = i;
                    while (i < n && Math.Abs(channelData[i]) >= _threshold) i++;
                    int end = i - 1;

                    if (end - start + 1 >= _minClippedLength)
                        segments.Add(new ClippingSegment { Start = start, End = end });
                }
                else i++;
            }
            return segments;
        }

        /// <summary>
        /// Собирает «здоровые» сэмплы вокруг клиппированного участка
        /// </summary>
        private InterpolationContext PrepareContext(float[] data, ClippingSegment segment)
        {
            var xList = new List<double>();
            var yList = new List<double>();
            int leftCount = 0;

            // Точки слева (берем SidePoints или сколько есть до начала файла)
            for (int i = segment.Start - SidePoints; i < segment.Start; i++)
            {
                if (i >= 0)
                {
                    xList.Add(i);
                    yList.Add(data[i]);
                    leftCount++;
                }
            }

            // Точки справа
            for (int i = segment.End + 1; i <= segment.End + SidePoints; i++)
            {
                if (i < data.Length)
                {
                    xList.Add(i);
                    yList.Add(data[i]);
                }
            }

            // Важно: для корректной интерполяции Эрмита нам нужно по крайней мере 2 точки с каждой стороны
            return new InterpolationContext
            {
                X = xList.ToArray(),
                Y = yList.ToArray(),
                Segment = segment,
                SidePointsActual = leftCount
            };
        }

        /// <summary>
        /// Восстанавливает значения внутри сегмента, используя кубическую интерполяцию Эрмита.
        /// </summary>
        private void Interpolate(float[] output, InterpolationContext ctx)
        {
            // Нам нужно хотя бы по 2 точки с каждой стороны для вычисления наклона
            if (ctx.SidePointsActual < 2 || (ctx.X.Length - ctx.SidePointsActual) < 2) return;

            // --- ЛЕВЫЙ КРАЙ (Точки перед клиппингом) ---
            double x0 = ctx.X[ctx.SidePointsActual - 2];
            double y0 = ctx.Y[ctx.SidePointsActual - 2];
            double x1 = ctx.X[ctx.SidePointsActual - 1]; // Точка прямо перед полкой
            double y1 = ctx.Y[ctx.SidePointsActual - 1];

            // --- ПРАВЫЙ КРАЙ (Точки после клиппинга) ---
            double x2 = ctx.X[ctx.SidePointsActual];     // Точка прямо после полки
            double y2 = ctx.Y[ctx.SidePointsActual];
            double x3 = ctx.X[ctx.SidePointsActual + 1];
            double y3 = ctx.Y[ctx.SidePointsActual + 1];

            // ИСПРАВЛЕНИЕ: Считаем наклон только по "здоровым" участкам
            // m1 — наклон входа в клиппинг (основан на точках слева)
            double m1 = (y1 - y0) / (x1 - x0);
            // m2 — наклон выхода из клиппинга (основан на точках справа)
            double m2 = (y3 - y2) / (x3 - x2);

            int start = ctx.Segment.Start;
            int end = ctx.Segment.End;
            double gapWidth = x2 - x1;

            for (int i = start; i <= end; i++)
            {
                double t = (i - x1) / gapWidth;
                double t2 = t * t;
                double t3 = t2 * t;

                // Полиномы Эрмита (H00, H10, H01, H11)
                double h00 = 2 * t3 - 3 * t2 + 1;
                double h10 = t3 - 2 * t2 + t;
                double h01 = -2 * t3 + 3 * t2;
                double h11 = t3 - t2;

                // Вычисляем значение. 
                // m1 и m2 умножаются на gapWidth для нормализации производной к интервалу [0,1]
                double restored = h00 * y1 + h10 * gapWidth * m1 + h01 * y2 + h11 * gapWidth * m2;

                output[i] = (float)restored;
            }
        }
    }
}