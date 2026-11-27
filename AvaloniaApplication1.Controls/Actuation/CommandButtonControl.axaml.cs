using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AvaloniaApplication1.Controls.Actuation
{
    public partial class CommandButtonControl : UserControl
    {
        public static readonly StyledProperty<ICommand?> CommandProperty =
            AvaloniaProperty.Register<CommandButtonControl, ICommand?>(nameof(Command));

        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<CommandButtonControl, string>(nameof(Title), string.Empty);

        public static readonly StyledProperty<bool> ShowTitleProperty =
            AvaloniaProperty.Register<CommandButtonControl, bool>(nameof(ShowTitle), false);

        public static readonly StyledProperty<bool> IsToggleSwitchModeProperty =
            AvaloniaProperty.Register<CommandButtonControl, bool>(nameof(IsToggleSwitchMode), false);

        public static readonly StyledProperty<bool> IsHorizontalProperty =
            AvaloniaProperty.Register<CommandButtonControl, bool>(nameof(IsHorizontal), true);

        public static readonly StyledProperty<bool> IsVerticalProperty =
            AvaloniaProperty.Register<CommandButtonControl, bool>(nameof(IsVertical), false);

        public static readonly StyledProperty<bool> IsPillStyleProperty =
            AvaloniaProperty.Register<CommandButtonControl, bool>(nameof(IsPillStyle), true);

        public static readonly StyledProperty<bool> IsRockerStyleProperty =
            AvaloniaProperty.Register<CommandButtonControl, bool>(nameof(IsRockerStyle), false);

        public static readonly StyledProperty<bool> IsLeverStyleProperty =
            AvaloniaProperty.Register<CommandButtonControl, bool>(nameof(IsLeverStyle), false);

        public static readonly StyledProperty<bool> IsActiveProperty =
            AvaloniaProperty.Register<CommandButtonControl, bool>(nameof(IsActive), false);

        public static readonly StyledProperty<string> LabelOnProperty =
            AvaloniaProperty.Register<CommandButtonControl, string>(nameof(LabelOn), "ВКЛ");

        public static readonly StyledProperty<string> LabelOffProperty =
            AvaloniaProperty.Register<CommandButtonControl, string>(nameof(LabelOff), "ВЫКЛ");

        public static readonly StyledProperty<string> CurrentLabelProperty =
            AvaloniaProperty.Register<CommandButtonControl, string>(nameof(CurrentLabel), string.Empty);

        public static readonly StyledProperty<IBrush?> ButtonBackgroundProperty =
            AvaloniaProperty.Register<CommandButtonControl, IBrush?>(nameof(ButtonBackground), Brushes.Transparent);

        public static readonly StyledProperty<IBrush?> BrushOnProperty =
            AvaloniaProperty.Register<CommandButtonControl, IBrush?>(nameof(BrushOn), Brushes.LimeGreen);

        public static readonly StyledProperty<IBrush?> BrushOffProperty =
            AvaloniaProperty.Register<CommandButtonControl, IBrush?>(nameof(BrushOff), Brushes.Gray);

        public static readonly StyledProperty<IBrush?> TrackBackgroundProperty =
            AvaloniaProperty.Register<CommandButtonControl, IBrush?>(nameof(TrackBackground), Brushes.DarkGray);

        public static readonly StyledProperty<IBrush?> TrackBorderBrushProperty =
            AvaloniaProperty.Register<CommandButtonControl, IBrush?>(nameof(TrackBorderBrush), Brushes.Gray);

        public static readonly StyledProperty<IBrush?> ActiveShoulderBgProperty =
            AvaloniaProperty.Register<CommandButtonControl, IBrush?>(nameof(ActiveShoulderBg), Brushes.DarkGray);

        public static readonly StyledProperty<IBrush?> InactiveShoulderBgProperty =
            AvaloniaProperty.Register<CommandButtonControl, IBrush?>(nameof(InactiveShoulderBg), Brushes.DarkGray);

        public static readonly StyledProperty<IBrush?> OnLabelBrushProperty =
            AvaloniaProperty.Register<CommandButtonControl, IBrush?>(nameof(OnLabelBrush), Brushes.White);

        public static readonly StyledProperty<IBrush?> OffLabelBrushProperty =
            AvaloniaProperty.Register<CommandButtonControl, IBrush?>(nameof(OffLabelBrush), Brushes.Gray);

        public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
        public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public bool ShowTitle { get => GetValue(ShowTitleProperty); set => SetValue(ShowTitleProperty, value); }
        public bool IsToggleSwitchMode { get => GetValue(IsToggleSwitchModeProperty); set => SetValue(IsToggleSwitchModeProperty, value); }
        public bool IsHorizontal { get => GetValue(IsHorizontalProperty); set => SetValue(IsHorizontalProperty, value); }
        public bool IsVertical { get => GetValue(IsVerticalProperty); set => SetValue(IsVerticalProperty, value); }
        public bool IsPillStyle { get => GetValue(IsPillStyleProperty); set => SetValue(IsPillStyleProperty, value); }
        public bool IsRockerStyle { get => GetValue(IsRockerStyleProperty); set => SetValue(IsRockerStyleProperty, value); }
        public bool IsLeverStyle { get => GetValue(IsLeverStyleProperty); set => SetValue(IsLeverStyleProperty, value); }
        public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
        public string LabelOn { get => GetValue(LabelOnProperty); set => SetValue(LabelOnProperty, value); }
        public string LabelOff { get => GetValue(LabelOffProperty); set => SetValue(LabelOffProperty, value); }
        public string CurrentLabel { get => GetValue(CurrentLabelProperty); set => SetValue(CurrentLabelProperty, value); }
        public IBrush? ButtonBackground { get => GetValue(ButtonBackgroundProperty); set => SetValue(ButtonBackgroundProperty, value); }
        public IBrush? BrushOn { get => GetValue(BrushOnProperty); set => SetValue(BrushOnProperty, value); }
        public IBrush? BrushOff { get => GetValue(BrushOffProperty); set => SetValue(BrushOffProperty, value); }
        public IBrush? TrackBackground { get => GetValue(TrackBackgroundProperty); set => SetValue(TrackBackgroundProperty, value); }
        public IBrush? TrackBorderBrush { get => GetValue(TrackBorderBrushProperty); set => SetValue(TrackBorderBrushProperty, value); }
        public IBrush? ActiveShoulderBg { get => GetValue(ActiveShoulderBgProperty); set => SetValue(ActiveShoulderBgProperty, value); }
        public IBrush? InactiveShoulderBg { get => GetValue(InactiveShoulderBgProperty); set => SetValue(InactiveShoulderBgProperty, value); }
        public IBrush? OnLabelBrush { get => GetValue(OnLabelBrushProperty); set => SetValue(OnLabelBrushProperty, value); }
        public IBrush? OffLabelBrush { get => GetValue(OffLabelBrushProperty); set => SetValue(OffLabelBrushProperty, value); }

        public CommandButtonControl()
        {
            InitializeComponent();
        }
    }
}
