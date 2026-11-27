using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace AvaloniaApplication1.Controls.Actuation
{
    public partial class SliderControl : UserControl
    {
        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<SliderControl, string>(nameof(Title), string.Empty);

        public static readonly StyledProperty<bool> ShowTitleProperty =
            AvaloniaProperty.Register<SliderControl, bool>(nameof(ShowTitle), true);

        public static readonly StyledProperty<double> ValueProperty =
            AvaloniaProperty.Register<SliderControl, double>(nameof(Value), 0.0, defaultBindingMode: BindingMode.TwoWay);

        public static readonly StyledProperty<double> MinimumProperty =
            AvaloniaProperty.Register<SliderControl, double>(nameof(Minimum), 0.0);

        public static readonly StyledProperty<double> MaximumProperty =
            AvaloniaProperty.Register<SliderControl, double>(nameof(Maximum), 100.0);

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

        public double Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double Minimum
        {
            get => GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public double Maximum
        {
            get => GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public SliderControl()
        {
            InitializeComponent();
        }
    }
}
