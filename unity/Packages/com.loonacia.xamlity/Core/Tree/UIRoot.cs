using System;
using Xamlity.Layout;

namespace Xamlity.Tree
{
    public sealed class UIRoot
    {
        public UIRoot(UIElement rootElement)
        {
            RootElement = rootElement ?? throw new ArgumentNullException(nameof(rootElement));
        }

        public UIElement RootElement { get; }

        public void UpdateLayout(Size viewportSize)
        {
            RootElement.Measure(viewportSize);
            RootElement.Arrange(new Rect(new Point(0, 0), viewportSize));
        }
    }
}
