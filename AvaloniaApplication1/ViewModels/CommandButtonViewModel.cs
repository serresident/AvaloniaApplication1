using System;
using System.Threading.Tasks;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels
{
    public partial class CommandButtonViewModel : WidgetViewModelBase
    {
        private static readonly IBrush BrightWhiteBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#FFFFFF"));
        private static readonly IBrush DimmedGrayBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#77777A"));
        private static readonly IBrush DefaultTrackBg = new SolidColorBrush(Avalonia.Media.Color.Parse("#2B2B30"));
        private static readonly IBrush DefaultTrackBorder = new SolidColorBrush(Avalonia.Media.Color.Parse("#505058"));
        private static readonly IBrush ActiveTrackBorder = new SolidColorBrush(Avalonia.Media.Color.Parse("#44FFFFFF"));
        private static readonly IBrush SwitchFaceplateBg = new SolidColorBrush(Avalonia.Media.Color.Parse("#212226"));
        private static readonly IBrush RaisedShoulderBg = new SolidColorBrush(Avalonia.Media.Color.Parse("#383A40"));
        private static readonly IBrush DepressedShoulderBg = new SolidColorBrush(Avalonia.Media.Color.Parse("#202124"));

        public CommandButtonConfig TypedConfig => (CommandButtonConfig)OriginalConfig;

        // ── State (for Latching/ToggleSwitch mode) ──
        [ObservableProperty]
        private bool _isActive;

        // ── Switch Style & Orientation ──
        [ObservableProperty]
        private string _switchStyle = "Pill";

        [ObservableProperty]
        private string _orientation = "Horizontal";

        /// <summary>True when ButtonMode == ToggleSwitch — switches to multi-style vector toggle display.</summary>
        public bool IsToggleSwitchMode => string.Equals(TypedConfig.ButtonMode, "ToggleSwitch", StringComparison.OrdinalIgnoreCase);
        public bool IsRockerStyle => string.Equals(SwitchStyle, "Rocker", StringComparison.OrdinalIgnoreCase);
        public bool IsLeverStyle => string.Equals(SwitchStyle, "Lever", StringComparison.OrdinalIgnoreCase);
        public bool IsPillStyle => !IsRockerStyle && !IsLeverStyle;
        public bool IsVertical => string.Equals(Orientation, "Vertical", StringComparison.OrdinalIgnoreCase);
        public bool IsHorizontal => !IsVertical;

        // ── Displayed label (reacts to IsActive for Latching) ──
        [ObservableProperty]
        private string _currentLabel = string.Empty;

        // ── Displayed background color ──
        [ObservableProperty]
        private IBrush _currentBackground = Brushes.DimGray;

        [ObservableProperty]
        private IBrush _buttonBackground = Brushes.DimGray;

        [ObservableProperty]
        private IBrush _brushOn = new SolidColorBrush(Avalonia.Media.Color.Parse("#00AA44"));

        [ObservableProperty]
        private IBrush _brushOff = new SolidColorBrush(Avalonia.Media.Color.Parse("#AA2200"));

        [ObservableProperty]
        private IBrush _onLabelBrush = BrightWhiteBrush;

        [ObservableProperty]
        private IBrush _offLabelBrush = DimmedGrayBrush;

        [ObservableProperty]
        private IBrush _trackBackground = DefaultTrackBg;

        [ObservableProperty]
        private IBrush _trackBorderBrush = DefaultTrackBorder;

        [ObservableProperty]
        private IBrush _activeShoulderBg = RaisedShoulderBg;

        [ObservableProperty]
        private IBrush _inactiveShoulderBg = DepressedShoulderBg;

        public CommandButtonViewModel(CommandButtonConfig config, IDataCoreService dataService, IProjectContextService projectContext)
            : base(config, dataService, projectContext)
        {
            _switchStyle = string.IsNullOrEmpty(config.SwitchStyle) ? "Pill" : config.SwitchStyle;
            _orientation = string.IsNullOrEmpty(config.Orientation) ? "Horizontal" : config.Orientation;
            SyncVisual();
        }

        partial void OnIsActiveChanged(bool value)
        {
            SyncVisual();
        }

        partial void OnSwitchStyleChanged(string value)
        {
            TypedConfig.SwitchStyle = value;
            OnPropertyChanged(nameof(IsPillStyle));
            OnPropertyChanged(nameof(IsRockerStyle));
            OnPropertyChanged(nameof(IsLeverStyle));
            SyncVisual();
        }

        partial void OnOrientationChanged(string value)
        {
            TypedConfig.Orientation = value;
            OnPropertyChanged(nameof(IsVertical));
            OnPropertyChanged(nameof(IsHorizontal));
            SyncVisual();
        }

        public void SyncVisual()
        {
            var cfg = TypedConfig;
            ParseBrush(cfg.ColorOn, out var bOn);
            BrushOn = bOn;
            ParseBrush(cfg.ColorOff, out var bOff);
            BrushOff = bOff;

            OnLabelBrush = IsActive ? BrightWhiteBrush : DimmedGrayBrush;
            OffLabelBrush = IsActive ? DimmedGrayBrush : BrightWhiteBrush;

            ActiveShoulderBg = IsActive ? RaisedShoulderBg : DepressedShoulderBg;
            InactiveShoulderBg = !IsActive ? RaisedShoulderBg : DepressedShoulderBg;

            if (cfg.ButtonMode == "Latching")
            {
                CurrentLabel = IsActive ? cfg.LabelOn : cfg.LabelOff;
                CurrentBackground = IsActive ? BrushOn : BrushOff;
                ButtonBackground = CurrentBackground;
            }
            else if (cfg.ButtonMode == "ToggleSwitch")
            {
                CurrentLabel = IsActive ? cfg.LabelOn : cfg.LabelOff;
                CurrentBackground = IsActive ? BrushOn : BrushOff;
                ButtonBackground = SwitchFaceplateBg;
                TrackBackground = IsActive ? BrushOn : DefaultTrackBg;
                TrackBorderBrush = IsActive ? ActiveTrackBorder : DefaultTrackBorder;
            }
            else
            {
                CurrentLabel = cfg.Title;
                CurrentBackground = new SolidColorBrush(Avalonia.Media.Color.Parse("#4A4A4D"));
                ButtonBackground = CurrentBackground;
            }
        }

        private static void ParseBrush(string hex, out IBrush brush)
        {
            brush = Brushes.DimGray;
            if (!string.IsNullOrWhiteSpace(hex) && Avalonia.Media.Color.TryParse(hex, out var c))
                brush = new SolidColorBrush(c);
        }

        protected override void OnTagValueUpdated(object newValue)
        {
            base.OnTagValueUpdated(newValue);
            if (TypedConfig.ButtonMode == "Latching" || TypedConfig.ButtonMode == "ToggleSwitch")
            {
                if (newValue is bool b)
                {
                    IsActive = b;
                }
                else if (newValue is int i)
                {
                    IsActive = i != 0;
                }
                else if (bool.TryParse(newValue?.ToString(), out var parsedBool))
                {
                    IsActive = parsedBool;
                }
            }
        }

        [RelayCommand]
        private async Task ExecuteCommandAsync()
        {
            if (ProjectContext.IsDesignMode || Source == null) return;

            var cfg = TypedConfig;
            string tagId = Source.TagId ?? ConfigMigrationService.GenerateTagId(Source.ConnId, Source.Address);

            // Safety & Operator Override check when communication quality is degraded
            if (IsStale || HasCommError)
            {
                if (DialogService != null)
                {
                    bool confirmed = await DialogService.ShowConfirmAsync(
                        "Подтверждение переопределения команды",
                        $"Качество связи нарушено ({Quality}). Вы уверены, что хотите принудительно отправить команду на {Source.ConnId}:{Source.Address}?");
                    if (!confirmed)
                    {
                        return;
                    }
                }

                IsOverridden = true;
                Quality = TagQuality.Overridden;
                DataService.ForceOverride(tagId, true);
            }

            // Operator Confirmation safety check (ISA-101 / Concepts Brief)
            if (cfg.RequireConfirmation && DialogService != null)
            {
                string targetState = (!IsActive) ? (cfg.LabelOn ?? "ВКЛ") : (cfg.LabelOff ?? "ВЫКЛ");
                bool confirmed = await DialogService.ShowConfirmAsync(
                    "Подтверждение переключения",
                    $"Вы уверены, что хотите переключить '{Title}' в состояние '{targetState}' ({Source.ConnId}:{Source.Address})?");
                if (!confirmed)
                {
                    return;
                }
            }

            switch (cfg.ButtonMode)
            {
                case "Latching":
                case "ToggleSwitch":
                    IsActive = !IsActive;
                    DataService.WriteCommand(Source.ConnId, Source.Address, IsActive);
                    break;

                case "Momentary":
                    DataService.WriteCommand(Source.ConnId, Source.Address, true);
                    break;

                default: // Toggle
                    var current = DataService.GetCurrentValue(Source.ConnId, Source.Address);
                    bool newState = true;
                    if (current is bool b) newState = !b;
                    else if (current is int i) newState = i == 0;
                    DataService.WriteCommand(Source.ConnId, Source.Address, newState);
                    break;
            }
            await Task.CompletedTask;
        }
    }
}
