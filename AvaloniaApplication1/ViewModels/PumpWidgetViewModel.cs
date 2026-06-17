using System;
using System.Collections.Generic;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace AvaloniaApplication1.ViewModels
{
    public partial class PumpWidgetViewModel : WidgetViewModelBase
    {
        public PumpConfig TypedConfig => (PumpConfig)OriginalConfig;

        [ObservableProperty]
        private bool _isRunning;

        [ObservableProperty]
        private string _displayValue = "STOPPED";

        [ObservableProperty]
        private string _currentColor = "#FF0000";

        public string ActiveColor => string.IsNullOrEmpty(TypedConfig.ActiveColor) ? "#00FF00" : TypedConfig.ActiveColor;
        public string InactiveColor => string.IsNullOrEmpty(TypedConfig.InactiveColor) ? "#FF0000" : TypedConfig.InactiveColor;

        public PumpWidgetViewModel(PumpConfig config, IDataCoreService dataService, IProjectContextService projectContext) 
            : base(config, dataService, projectContext)
        {
            UpdateState();
        }

        protected override void OnTagValueUpdated(object newValue)
        {
            UpdateState();
        }

        private void UpdateState()
        {
            if (Source == null)
            {
                CurrentColor = InactiveColor;
                return;
            }

            var val = DataService.GetCurrentValue(Source.ConnId, Source.Address);
            if (val != null)
            {
                if (val is bool b) IsRunning = b;
                else if (val is int i) IsRunning = i > 0;
                else if (double.TryParse(val.ToString(), out double num)) IsRunning = num > 0;

                DisplayValue = IsRunning ? "RUNNING" : "STOPPED";
                CurrentColor = IsRunning ? ActiveColor : InactiveColor;
            }
            else
            {
                DisplayValue = "STOPPED";
                CurrentColor = InactiveColor;
            }
        }

        private ChildWindowViewModel? _controlWindow;

        [RelayCommand]
        private void OpenControlPopup()
        {
            if (ProjectContext.IsDesignMode || Source == null) return;

            var mainVm = App.Services?.GetService<MainViewModel>();
            if (mainVm == null) return;

            var titleToFind = $"{Title} [Control]";
            var existing = mainVm.ActiveChildWindows.FirstOrDefault(w => w.Title == titleToFind);
            if (existing != null)
            {
                existing.CloseAction?.Invoke();
                _controlWindow = null;
                return;
            }

            var widgets = new List<WidgetConfig>
            {
                // Status Display
                new ValueDisplayConfig
                {
                    Type = "ValueDisplay",
                    Title = "Pump Status",
                    Source = new DataSourceConfig { ConnId = Source.ConnId, Address = Source.Address, DataType = Source.DataType },
                    Position = new WidgetPosition { Row = 0, Col = 0, SizeX = 2, SizeY = 1 },
                    Format = "Status: {0}"
                },

                // Start/Stop Toggle Button
                new CommandButtonConfig
                {
                    Type = "CommandButton",
                    Title = "START / STOP",
                    Source = new DataSourceConfig { ConnId = Source.ConnId, Address = Source.Address, DataType = Source.DataType },
                    Position = new WidgetPosition { Row = 1, Col = 0, SizeX = 2, SizeY = 1 },
                    ButtonMode = "Toggle"
                }
            };

            var dashboardConfig = new DashboardConfig
            {
                Widgets = widgets
            };

            _controlWindow = mainVm.OpenChildWindow($"{Title} [Control]", dashboardConfig);
            if (_controlWindow != null)
            {
                var originalClose = _controlWindow.CloseAction;
                _controlWindow.CloseAction = () =>
                {
                    originalClose?.Invoke();
                    _controlWindow = null;
                };
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
