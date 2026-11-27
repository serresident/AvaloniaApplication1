using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AvaloniaApplication1.Controls.Indication
{
    public partial class ValueDisplayControl : UserControl
    {
        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<ValueDisplayControl, string>(nameof(Title), string.Empty);

        public static readonly StyledProperty<bool> ShowTitleProperty =
            AvaloniaProperty.Register<ValueDisplayControl, bool>(nameof(ShowTitle), true);

        public static readonly StyledProperty<string> DisplayValueProperty =
            AvaloniaProperty.Register<ValueDisplayControl, string>(nameof(DisplayValue), "---");

        public static readonly StyledProperty<IBrush?> ValueBrushProperty =
            AvaloniaProperty.Register<ValueDisplayControl, IBrush?>(nameof(ValueBrush), Brushes.White);

        public string Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public bool ShowTitle
        {
            get => GetValue(ShowTitleProperty);
            set => SetValue(ShowTitleProperty, value);
        }

        public string DisplayValue
        {
            get => GetValue(DisplayValueProperty);
            set => SetValue(DisplayValueProperty, value);
        }

        public IBrush? ValueBrush
        {
            get => GetValue(ValueBrushProperty);
            set => SetValue(ValueBrushProperty, value);
        }

        public ValueDisplayControl()
        {
            InitializeComponent();
        }
    }
}
