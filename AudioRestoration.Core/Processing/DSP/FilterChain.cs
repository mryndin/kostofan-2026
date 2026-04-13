using System.Collections.Generic;

namespace AudioRestoration.Core.Processing.DSP
{
    public class FilterChain : ISoundFilter
    {
        private readonly List<ISoundFilter> _filters = new();

        public void AddFilter(ISoundFilter filter) => _filters.Add(filter);

        public float Process(float input)
        {
            float output = input;
            // Прогоняем сэмпл через всю цепочку последовательно
            foreach (var filter in _filters)
            {
                output = filter.Process(output);
            }
            return output;
        }

        public void Reset()
        {
            foreach (var filter in _filters) filter.Reset();
        }
    }
}