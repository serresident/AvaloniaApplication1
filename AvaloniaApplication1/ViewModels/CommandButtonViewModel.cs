using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels
{
    public partial class CommandButtonViewModel : WidgetViewModelBase
    {
        public CommandButtonConfig TypedConfig => (CommandButtonConfig)OriginalConfig;

        // ── State (for Latching/ToggleSwitch mode) ──
        [ObservableProperty]
        private bool _isActive;

        // ── Displayed label (reacts to IsActive for Latching) ──
        [ObservableProperty]
        private string _currentLabel = string.Empty;

        // ── Displayed background color ──
        [ObservableProperty]
        private IBrush _currentBackground = Brushes.DimGray;

        /// <summary>True when ButtonMode == ToggleSwitch — shows left/right edge labels in UI.</summary>
        public bool IsToggleSwitchMode => TypedConfig.ButtonMode == "ToggleSwitch";

        public CommandButtonViewModel(CommandButtonConfig config, IDataCoreService dataService, IProjectContextService projectContext)
            : base(config, dataService, projectContext)
        {
            SyncVisual();
        }

        private void SyncVisual()
        {
            var cfg = TypedConfig;
            if (cfg.ButtonMode == "Latching")
            {
                CurrentLabel = IsActive ? cfg.LabelOn : cfg.LabelOff;
                ParseBrush(IsActive ? cfg.ColorOn : cfg.ColorOff, out var br);
                CurrentBackground = br;
            }
            else if (cfg.ButtonMode == "ToggleSwitch")
            {
                // Label shown on the button body = current state label
                CurrentLabel = IsActive ? cfg.LabelOn : cfg.LabelOff;
                ParseBrush(IsActive ? cfg.ColorOn : cfg.ColorOff, out var br);
                CurrentBackground = br;
            }
            else
            {
                // Toggle / Momentary — just show title
                CurrentLabel = cfg.Title;
                CurrentBackground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4A4A4D"));
            }
        }

        private static void ParseBrush(string hex, out IBrush brush)
        {
            brush = Brushes.DimGray;
            if (!string.IsNullOrWhiteSpace(hex) && Avalonia.Media.Color.TryParse(hex, out var c))
                brush = new SolidColorBrush(c);
        }

        [RelayCommand]
        private void ExecuteCommand()
        {
            if (ProjectContext.IsDesignMode || Source == null) return;

            var cfg = TypedConfig;
            switch (cfg.ButtonMode)
            {
                case "Latching":
                    // Toggle the latch state
                    IsActive = !IsActive;
                    DataService.WriteCommand(Source.ConnId, Source.Address, IsActive);
                    SyncVisual();
                    break;

                case "ToggleSwitch":
                    // Same as Latching but visual is a switch
                    IsActive = !IsActive;
                    DataService.WriteCommand(Source.ConnId, Source.Address, IsActive);
                    SyncVisual();
                    break;

                case "Momentary":
                    // Send true; caller should handle release separately
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
        }
    }
}
