using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AvaloniaApplication1.Controls.Actuation
{
    public partial class SetValueControl : UserControl
    {
        public static readonly StyledProperty<ICommand?> CommandProperty =
            AvaloniaProperty.Register<SetValueControl, ICommand?>(nameof(Command));

        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<SetValueControl, string>(nameof(Title), string.Empty);

        public static readonly StyledProperty<bool> ShowTitleProperty =
            AvaloniaProperty.Register<SetValueControl, bool>(nameof(ShowTitle), true);

        public static readonly StyledProperty<string> DisplayValueProperty =
            AvaloniaProperty.Register<SetValueControl, string>(nameof(DisplayValue), "0");

        public static readonly StyledProperty<IBrush?> ValueBrushProperty =
            AvaloniaProperty.Register<SetValueControl, IBrush?>(nameof(ValueBrush), Brushes.White);

        public ICommand? Command
        {
            get => GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

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

        public SetValueControl()
        {
            InitializeComponent();
        }
    }
}
