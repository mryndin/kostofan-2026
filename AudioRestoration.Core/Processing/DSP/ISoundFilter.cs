namespace AudioRestoration.Core.Processing.DSP
{
    public interface ISoundFilter
    {
        // Обработка одного сэмпла
        float Process(float input);
        
        // Сброс внутренних состояний (буферов истории)
        void Reset();
    }
}