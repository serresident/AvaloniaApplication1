using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using Avalonia.Threading;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using ReactiveUI;
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

        [ObservableProperty]
        private bool _isVertical;

        [ObservableProperty]
        private string _actuatorType = "Solenoid";

        // --- New HMI Properties ---

        [ObservableProperty]
        private int _rotation;

        [ObservableProperty]
        private double _tolerance = 10.0;

        [ObservableProperty]
        private bool _alarmDisabled;

        [ObservableProperty]
        private bool _isAlarmActive;

        [ObservableProperty]
        private bool _isAlarmFlashing;

        [ObservableProperty]
        private bool _isAlarmFlashState;

        [ObservableProperty]
        private bool _showStaticAlarmIcon;

        [ObservableProperty]
        private bool _isManualMode = true;

        public string ValveType => string.IsNullOrEmpty(OriginalConfig.ValveType) ? "CutOff" : OriginalConfig.ValveType;
        public string ActiveColor => string.IsNullOrEmpty(OriginalConfig.ActiveColor) ? "#00FF00" : OriginalConfig.ActiveColor;
        public string InactiveColor => string.IsNullOrEmpty(OriginalConfig.InactiveColor) ? "#FF0000" : OriginalConfig.InactiveColor;

        public bool IsRegulating => string.Equals(ValveType, "Regulating", StringComparison.OrdinalIgnoreCase);

        private readonly DispatcherTimer _alarmTimer;
        private bool _wasAlarmActive;

        private double? _lastPopupX;
        private double? _lastPopupY;

        public ValveWidgetViewModel(WidgetConfig config, IMockDataService dataService, IProjectContextService projectContext) 
            : base(config, dataService, projectContext)
        {
            HasFeedbackSource = !string.IsNullOrEmpty(config.FeedbackSource?.Address);
            IsVertical = config.IsVertical;
            ActuatorType = string.IsNullOrEmpty(config.ActuatorType) ? "Solenoid" : config.ActuatorType;

            // Load new config values
            Rotation = config.Rotation;
            Tolerance = config.Tolerance == 0 ? 10.0 : config.Tolerance;
            AlarmDisabled = config.AlarmDisabled;

            // Initialize Mode
            UpdateModeState();

            // Set up 500ms alarm flashing timer
            _alarmTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _alarmTimer.Tick += (s, e) =>
            {
                if (IsAlarmFlashing)
                {
                    IsAlarmFlashState = !IsAlarmFlashState;
                }
                else
                {
                    IsAlarmFlashState = false;
                }
            };
            _alarmTimer.Start();

            // Set up reactive subscriptions for extra sources
            var fbSource = OriginalConfig.FeedbackSource;
            if (fbSource != null && !string.IsNullOrEmpty(fbSource.ConnId) && !string.IsNullOrEmpty(fbSource.Address))
            {
                DataService.TagUpdates
                    .Where(t => t.ConnId == fbSource.ConnId && t.Address == fbSource.Address)
                    .Sample(TimeSpan.FromMilliseconds(100))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(_ => UpdateFeedback())
                    .DisposeWith(Disposables);
            }

            var modeSrc = OriginalConfig.ModeSource;
            if (modeSrc != null && !string.IsNullOrEmpty(modeSrc.ConnId) && !string.IsNullOrEmpty(modeSrc.Address))
            {
                DataService.TagUpdates
                    .Where(t => t.ConnId == modeSrc.ConnId && t.Address == modeSrc.Address)
                    .Sample(TimeSpan.FromMilliseconds(100))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(_ => UpdateModeState())
                    .DisposeWith(Disposables);
            }

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
                if (!IsRegulating)
                {
                    if (val is bool b) IsOpen = b;
                    else if (val is int i) IsOpen = i > 0;
                    else if (double.TryParse(val.ToString(), out double num)) IsOpen = num > 0;
                    
                    DisplayValue = IsOpen ? "ОТКРЫТ" : "ЗАКРЫТ";
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

            EvaluateAlarm();
        }

        private void UpdateFeedback()
        {
            var fbSource = OriginalConfig.FeedbackSource;
            if (fbSource == null || string.IsNullOrEmpty(fbSource.Address)) return;

            var fbVal = DataService.GetCurrentValue(fbSource.ConnId, fbSource.Address);
            if (fbVal != null)
            {
                if (IsRegulating)
                {
                    if (double.TryParse(fbVal.ToString(), out double dFb))
                    {
                        Feedback = Math.Clamp(dFb, 0, 100);
                    }
                }
                else // CutOff
                {
                    if (fbVal is bool b) IsOpen = b;
                    else if (fbVal is int i) IsOpen = i > 0;
                    else if (double.TryParse(fbVal.ToString(), out double num)) IsOpen = num > 0;
                    Feedback = IsOpen ? 100 : 0;
                }
            }

            EvaluateAlarm();
        }

        private void UpdateModeState()
        {
            var modeSrc = OriginalConfig.ModeSource;
            if (modeSrc == null || string.IsNullOrEmpty(modeSrc.Address))
            {
                IsManualMode = true;
                return;
            }

            var mVal = DataService.GetCurrentValue(modeSrc.ConnId, modeSrc.Address);
            if (mVal != null)
            {
                // auto = true, manual = false
                if (mVal is bool b) IsManualMode = !b;
                else if (mVal is int i) IsManualMode = (i == 0);
                else if (double.TryParse(mVal.ToString(), out double num)) IsManualMode = (num == 0);
            }
            else
            {
                IsManualMode = true;
            }
        }

        partial void OnIsManualModeChanged(bool value)
        {
            var modeSrc = OriginalConfig.ModeSource;
            if (modeSrc != null && !string.IsNullOrEmpty(modeSrc.Address))
            {
                object writeVal = !value; // Auto = true, Manual = false
                if (modeSrc.DataType == "Float32" || modeSrc.DataType == "Float")
                    writeVal = !value ? 1.0f : 0.0f;
                else if (modeSrc.DataType == "Int16" || modeSrc.DataType == "Int32")
                    writeVal = !value ? 1 : 0;

                DataService.WriteCommand(modeSrc.ConnId, modeSrc.Address, writeVal);
            }
        }

        partial void OnAlarmDisabledChanged(bool value)
        {
            // Persist locally in config if desired, but keep in memory for now
            OriginalConfig.AlarmDisabled = value;
            EvaluateAlarm();
        }

        private void EvaluateAlarm()
        {
            if (!HasFeedbackSource)
            {
                IsAlarmActive = false;
                IsAlarmFlashing = false;
                ShowStaticAlarmIcon = false;
                return;
            }

            if (IsRegulating)
            {
                IsAlarmActive = Math.Abs(Setpoint - Feedback) > Tolerance;
            }
            else
            {
                IsAlarmActive = (Setpoint > 0) != IsOpen;
            }

            IsAlarmFlashing = IsAlarmActive && !AlarmDisabled;
            ShowStaticAlarmIcon = IsAlarmActive && AlarmDisabled;

            // Trigger Toast Notification on transition from inactive to active
            if (IsAlarmActive && !_wasAlarmActive)
            {
                _wasAlarmActive = true;
                if (!AlarmDisabled)
                {
                    var mainVm = App.Services?.GetService<MainViewModel>();
                    if (mainVm != null)
                    {
                        string ctrlStr = IsRegulating ? $"{Setpoint:F0}%" : (Setpoint > 0 ? "ОТКРЫТ" : "ЗАКРЫТ");
                        string fbStr = IsRegulating ? $"{Feedback:F0}%" : (IsOpen ? "ОТКРЫТ" : "ЗАКРЫТ");
                        
                        mainVm.ShowToast($"Ошибка рассогласования [{Title}]: управление = {ctrlStr}, обратная связь = {fbStr}");
                    }
                }
            }
            else if (!IsAlarmActive)
            {
                _wasAlarmActive = false;
            }
        }

        private ChildWindowViewModel? _controlWindow;

        [RelayCommand]
        private void OpenControlPopup()
        {
            if (ProjectContext.IsDesignMode || Source == null) return;

            var mainVm = App.Services?.GetService<MainViewModel>();
            if (mainVm == null) return;

            var titleToFind = $"Управление клапаном {Title}";
            var existing = mainVm.ActiveChildWindows.FirstOrDefault(w => w.Title == titleToFind);
            if (existing != null)
            {
                existing.CloseCommand.Execute(null);
                _controlWindow = null;
                return;
            }

            // Create window frame
            _controlWindow = mainVm.OpenChildWindow(titleToFind, new object());
            if (_controlWindow != null)
            {
                // Position window
                if (_lastPopupX.HasValue && _lastPopupY.HasValue)
                {
                    _controlWindow.X = _lastPopupX.Value;
                    _controlWindow.Y = _lastPopupY.Value;
                }
                else
                {
                    double cellWidth = mainVm.Dashboard?.CellWidth ?? 150;
                    double cellHeight = mainVm.Dashboard?.CellHeight ?? 150;
                    double valveX = Col * cellWidth;
                    double valveY = Row * cellHeight;
                    double valveHeight = SizeY * cellHeight;

                    _controlWindow.X = Math.Max(10, valveX - 80);
                    _controlWindow.Y = Math.Max(10, valveY + valveHeight + 10);
                }

                // Instantiate Popup VM and link to window Content
                var popupVm = new ValveControlPopupViewModel(this, () => _controlWindow?.CloseCommand.Execute(null));
                _controlWindow.Content = popupVm;

                // Adjust width dynamically
                int baseWidth = IsRegulating ? 450 : 340;
                _controlWindow.Width = popupVm.IsKeypadVisible ? (baseWidth + 190) : baseWidth;
                _controlWindow.Height = IsRegulating ? 400 : 350;

                var originalClose = _controlWindow.CloseAction;
                _controlWindow.CloseAction = () =>
                {
                    if (_controlWindow != null)
                    {
                        _lastPopupX = _controlWindow.X;
                        _lastPopupY = _controlWindow.Y;
                    }

                    originalClose?.Invoke();
                    popupVm.Dispose();
                    _controlWindow = null;
                };

                // Track keypad visibility to expand or contract window dynamically
                popupVm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ValveControlPopupViewModel.IsKeypadVisible) && _controlWindow != null)
                    {
                        int currentBase = IsRegulating ? 450 : 340;
                        _controlWindow.Width = popupVm.IsKeypadVisible ? (currentBase + 190) : currentBase;
                    }
                };
            }
        }

        [RelayCommand]
        private void Rotate()
        {
            Rotation = (Rotation + 90) % 360;
            OriginalConfig.Rotation = Rotation;
        }

        public override void Dispose()
        {
            _alarmTimer.Stop();
            base.Dispose();
        }
    }
}
