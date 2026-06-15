using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace AvaloniaApplication1.ViewModels
{
    public partial class ValveWidgetViewModel : WidgetViewModelBase
    {
        [ObservableProperty]
        private bool _isOpen;

        [ObservableProperty]
        private double _currentValue;

        [ObservableProperty]
        private string _displayValue = "---";

        [ObservableProperty]
        private string _currentColor = "#FF0000";

        // Regulating valve properties
        [ObservableProperty]
        private double _setpoint;

        [ObservableProperty]
        private double _feedback;

        [ObservableProperty]
        private bool _hasFeedbackSource;

        public string ValveType => string.IsNullOrEmpty(OriginalConfig.ValveType) ? "CutOff" : OriginalConfig.ValveType;
        public string ActiveColor => string.IsNullOrEmpty(OriginalConfig.ActiveColor) ? "#00FF00" : OriginalConfig.ActiveColor;
        public string InactiveColor => string.IsNullOrEmpty(OriginalConfig.InactiveColor) ? "#FF0000" : OriginalConfig.InactiveColor;

        /// <summary>
        /// Control window dimensions from config.
        /// </summary>
        public double ControlWindowWidth => OriginalConfig.ControlWindowWidth > 0 ? OriginalConfig.ControlWindowWidth : 320;
        public double ControlWindowHeight => OriginalConfig.ControlWindowHeight > 0 ? OriginalConfig.ControlWindowHeight : 280;

        public ValveWidgetViewModel(WidgetConfig config, IMockDataService dataService, IProjectContextService projectContext) 
            : base(config, dataService, projectContext)
        {
            HasFeedbackSource = !string.IsNullOrEmpty(config.FeedbackSource?.Address);
            DataService.TagValueChanged += OnTagValueChanged;
            UpdateState();
        }

        private void OnTagValueChanged(object? sender, (string ConnId, string Address, object Value) e)
        {
            if (Source != null && e.ConnId == Source.ConnId && e.Address == Source.Address)
            {
                Dispatcher.UIThread.Post(UpdateState);
            }

            // Handle feedback source separately
            var fbSource = OriginalConfig.FeedbackSource;
            if (fbSource != null && !string.IsNullOrEmpty(fbSource.Address) &&
                e.ConnId == fbSource.ConnId && e.Address == fbSource.Address)
            {
                Dispatcher.UIThread.Post(UpdateFeedback);
            }
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
                if (ValveType == "CutOff")
                {
                    if (val is bool b) IsOpen = b;
                    else if (val is int i) IsOpen = i > 0;
                    else if (double.TryParse(val.ToString(), out double num)) IsOpen = num > 0;
                    
                    DisplayValue = IsOpen ? "OPEN" : "CLOSED";
                    CurrentColor = IsOpen ? ActiveColor : InactiveColor;
                    Setpoint = IsOpen ? 100 : 0;
                }
                else // Regulating
                {
                    if (double.TryParse(val.ToString(), out double dVal))
                    {
                        CurrentValue = dVal;
                        Setpoint = Math.Clamp(dVal, 0, 100);
                        DisplayValue = $"{CurrentValue:F1} %";
                        CurrentColor = CurrentValue > 0 ? ActiveColor : InactiveColor;
                    }
                }
            }
            else
            {
                DisplayValue = "---";
                CurrentColor = InactiveColor;
                Setpoint = 0;
            }

            // If no separate feedback source, feedback mirrors setpoint
            if (!HasFeedbackSource)
            {
                Feedback = Setpoint;
            }
            else
            {
                UpdateFeedback();
            }
        }

        private void UpdateFeedback()
        {
            var fbSource = OriginalConfig.FeedbackSource;
            if (fbSource == null || string.IsNullOrEmpty(fbSource.Address)) return;

            var fbVal = DataService.GetCurrentValue(fbSource.ConnId, fbSource.Address);
            if (fbVal != null && double.TryParse(fbVal.ToString(), out double dFb))
            {
                Feedback = Math.Clamp(dFb, 0, 100);
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

            var widgets = new List<WidgetConfig>();

            if (ValveType == "CutOff")
            {
                // Status Display
                widgets.Add(new WidgetConfig
                {
                    Type = "ValueDisplay",
                    Title = "Valve Status",
                    Source = new DataSourceConfig { ConnId = Source.ConnId, Address = Source.Address, DataType = Source.DataType },
                    Position = new WidgetPosition { Row = 0, Col = 0, SizeX = 2, SizeY = 1 },
                    Format = "State: {0}"
                });

                // Toggle Open/Close Button
                widgets.Add(new WidgetConfig
                {
                    Type = "CommandButton",
                    Title = "OPEN / CLOSE",
                    Source = new DataSourceConfig { ConnId = Source.ConnId, Address = Source.Address, DataType = Source.DataType },
                    Position = new WidgetPosition { Row = 1, Col = 0, SizeX = 2, SizeY = 1 },
                    ButtonMode = "Toggle"
                });
            }
            else // Regulating
            {
                // Status Display
                widgets.Add(new WidgetConfig
                {
                    Type = "ValueDisplay",
                    Title = "Opening Setpoint",
                    Source = new DataSourceConfig { ConnId = Source.ConnId, Address = Source.Address, DataType = Source.DataType },
                    Position = new WidgetPosition { Row = 0, Col = 0, SizeX = 2, SizeY = 1 },
                    Format = "{0:F1} %"
                });

                // Setpoint Input button
                widgets.Add(new WidgetConfig
                {
                    Type = "SetValue",
                    Title = "Set Position",
                    Source = new DataSourceConfig { ConnId = Source.ConnId, Address = Source.Address, DataType = Source.DataType },
                    Position = new WidgetPosition { Row = 1, Col = 0, SizeX = 1, SizeY = 1 },
                    Format = "{0:F1} %",
                    MinValue = 0,
                    MaxValue = 100
                });

                // Slider Input
                widgets.Add(new WidgetConfig
                {
                    Type = "Slider",
                    Title = "Slide Adjust",
                    Source = new DataSourceConfig { ConnId = Source.ConnId, Address = Source.Address, DataType = Source.DataType },
                    Position = new WidgetPosition { Row = 1, Col = 1, SizeX = 1, SizeY = 1 },
                    MinValue = 0,
                    MaxValue = 100
                });
            }

            var dashboardConfig = new DashboardConfig
            {
                Widgets = widgets
            };

            _controlWindow = mainVm.OpenChildWindow($"{Title} [Control]", dashboardConfig);
            if (_controlWindow != null)
            {
                _controlWindow.Width = ControlWindowWidth;
                _controlWindow.Height = ControlWindowHeight;
                
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
            DataService.TagValueChanged -= OnTagValueChanged;
            base.Dispose();
        }
    }
}
