using Avalonia;
using Avalonia.Controls;
using Avalonia.Metadata;

namespace AvaloniaApplication1.Views.Canvas.Overlays
{
    public partial class WidgetChrome : UserControl
    {
        public static readonly StyledProperty<object?> InnerContentProperty =
            AvaloniaProperty.Register<WidgetChrome, object?>(nameof(InnerContent));

        public static readonly StyledProperty<Thickness> CardPaddingProperty =
            AvaloniaProperty.Register<WidgetChrome, Thickness>(nameof(CardPadding), new Thickness(0));

        [Content]
        public object? InnerContent
        {
            get => GetValue(InnerContentProperty);
            set => SetValue(InnerContentProperty, value);
        }

        public Thickness CardPadding
        {
            get => GetValue(CardPaddingProperty);
            set => SetValue(CardPaddingProperty, value);
        }

        public WidgetChrome()
        {
            InitializeComponent();
        }
    }
}
