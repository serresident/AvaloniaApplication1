using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace AvaloniaApplication1.Controls.Actuation
{
    public partial class ContainerButtonControl : UserControl
    {
        public static readonly StyledProperty<ICommand?> CommandProperty =
            AvaloniaProperty.Register<ContainerButtonControl, ICommand?>(nameof(Command));

        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<ContainerButtonControl, string>(nameof(Title), string.Empty);

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

        public ContainerButtonControl()
        {
            InitializeComponent();
        }
    }
}
