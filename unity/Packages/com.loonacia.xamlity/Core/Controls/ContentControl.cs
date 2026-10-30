using Xamlity.Markup;
using Xamlity.Properties;

namespace Xamlity.Controls
{
    [ContentProperty("Content")]
    public class ContentControl : Control
    {
        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
            nameof(Content), typeof(object), typeof(ContentControl), new PropertyMetadata(null));

        public ContentControl() => DefaultStyleKey = typeof(ContentControl);

        public object Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }
    }
}
