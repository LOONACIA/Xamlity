namespace Xamlity.Rendering
{
    public readonly struct FontMetrics
    {
        public FontMetrics(double ascent, double descent, double lineHeight)
        {
            Ascent = ascent;
            Descent = descent;
            LineHeight = lineHeight;
        }

        public double Ascent { get; }
        public double Descent { get; }
        public double LineHeight { get; }
    }
}
