namespace Xamlity.Rendering
{
    public sealed class SolidColorBrush : Brush
    {
        public SolidColorBrush(Color color)
        {
            Color = color;
        }

        public Color Color { get; }
    }
}
