using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using AvaloniaApplication1.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels.Widgets
{
    public class CrosshairSliceItem
    {
        public string PenName { get; set; } = string.Empty;
        public string ColorHex { get; set; } = "#00FFCC";
        public IBrush PenBrush { get; set; } = Brushes.Aqua;
        public string FormattedValue { get; set; } = string.Empty;
        public string Units { get; set; } = string.Empty;
    }

    public partial class RealTimeTrendViewModel : WidgetViewModelBase
    {
        public const int BufferCapacity = 120;

        public RealTimeTrendConfig TypedConfig => (RealTimeTrendConfig)OriginalConfig;

        public ObservableCollection<TrendPenViewModel> Pens { get; } = new();

        public ObservableCollection<CrosshairSliceItem> CrosshairValues { get; } = new();

        private DispatcherTimer? _samplingTimer;

        #region Observable Properties

        [ObservableProperty]
        private bool _isPaused;

        [ObservableProperty]
        private long _sampleTick;

        [ObservableProperty]
        private int _selectedSliceIndex = -1;

        [ObservableProperty]
        private bool _isCrosshairActive;

        [ObservableProperty]
        private double _crosshairX = -1;

        [ObservableProperty]
        private string _crosshairTimeText = string.Empty;

        [ObservableProperty]
        private double _leftMinY = 0.0;

        [ObservableProperty]
        private double _leftMaxY = 100.0;

        [ObservableProperty]
        private double _leftMidY = 50.0;

        [ObservableProperty]
        private string _leftUnits = string.Empty;

        [ObservableProperty]
        private double _rightMinY = 0.0;

        [ObservableProperty]
        private double _rightMaxY = 100.0;

        [ObservableProperty]
        private double _rightMidY = 50.0;

        [ObservableProperty]
        private string _rightUnits = string.Empty;

        [ObservableProperty]
        private bool _hasRightAxis;

        [ObservableProperty]
        private string _timeStartLabel = "-60с";

        [ObservableProperty]
        private string _timeMidLabel = "-30с";

        [ObservableProperty]
        private string _timeEndLabel = "Сейчас";

        #endregion

        #region Legacy / Compatibility Getters

        public IReadOnlyList<double>? Values => Pens.Count > 0 ? Pens[0].Values : null;

        public double MinY => LeftMinY;
        public double MaxY => LeftMaxY;

        public string ScalingMode => TypedConfig.ScalingMode;

        public string Format => string.IsNullOrEmpty(TypedConfig.Format) ? "{0:F1}" : TypedConfig.Format;

        public string LatestValueDisplay => Pens.Count > 0 ? Pens[0].LatestFormatted : "---";

        public bool ShowSetpoint => TypedConfig.ShowSetpoint;
        public double SetpointValue => TypedConfig.SetpointValue;
        public bool ShowWarningLimits => TypedConfig.ShowWarningLimits;
        public double WarningHigh => TypedConfig.WarningHigh;
        public double WarningLow => TypedConfig.WarningLow;
        public bool ShowAlarmLimits => TypedConfig.ShowAlarmLimits;
        public double AlarmHigh => TypedConfig.AlarmHigh;
        public double AlarmLow => TypedConfig.AlarmLow;
        public bool EnableCrosshair => TypedConfig.EnableCrosshair;

        public bool ShowLegendTable => TypedConfig.ShowLegendTable;
        public bool ShowLegendLatestValue => TypedConfig.ShowLegendLatestValue;
        public bool ShowLegendMinMax => TypedConfig.ShowLegendMinMax;
        public bool ShowLegendUnits => TypedConfig.ShowLegendUnits;

        /// <summary>
        /// ISA-101 Responsive Disclosure: collapse legend table when widget height is compact (SizeY &lt; 12)
        /// </summary>
        public bool IsLegendEffectiveVisible => TypedConfig.ShowLegendTable && SizeY >= 12;

        #endregion

        public RealTimeTrendViewModel(RealTimeTrendConfig config, IDataCoreService dataService, IProjectContextService projectContext) 
            : base(config, dataService, projectContext)
        {
            TypedConfig.EnsureDefaultPen();

            foreach (var penCfg in TypedConfig.Pens)
            {
                Pens.Add(new TrendPenViewModel(penCfg, DataService, BufferCapacity));
            }

            UpdateTimeLabels();
            UpdateAxesScales();

            int intervalMs = GetSamplingIntervalMs();
            _samplingTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(intervalMs),
                DispatcherPriority.Normal,
                (s, e) => OnSamplingTick());
            _samplingTimer.Start();
        }

        private int GetSamplingIntervalMs()
        {
            int winSec = TypedConfig.TimeWindowSeconds > 0 ? TypedConfig.TimeWindowSeconds : 60;
            return Math.Max(50, (winSec * 1000) / BufferCapacity);
        }

        private void UpdateTimeLabels()
        {
            int winSec = TypedConfig.TimeWindowSeconds > 0 ? TypedConfig.TimeWindowSeconds : 60;
            var now = DateTime.Now;
            TimeStartLabel = (now - TimeSpan.FromSeconds(winSec)).ToString("HH:mm:ss");
            TimeMidLabel = (now - TimeSpan.FromSeconds(winSec / 2.0)).ToString("HH:mm:ss");
            TimeEndLabel = now.ToString("HH:mm:ss");
        }

        private void OnSamplingTick()
        {
            if (IsPaused) return;

            for (int i = 0; i < Pens.Count; i++)
            {
                Pens[i].PushSample();
            }

            SampleTick++;
            UpdateTimeLabels();
            UpdateAxesScales();
            OnPropertyChanged(nameof(Values));
            OnPropertyChanged(nameof(LatestValueDisplay));

            if (IsCrosshairActive && SelectedSliceIndex >= 0)
            {
                UpdateCrosshairSlice();
            }
        }

        [RelayCommand]
        public void TogglePause()
        {
            IsPaused = !IsPaused;
        }

        public void UpdateAxesScales()
        {
            double leftMin = double.MaxValue;
            double leftMax = double.MinValue;
            string leftUnits = "";
            bool hasLeft = false;

            double rightMin = double.MaxValue;
            double rightMax = double.MinValue;
            string rightUnits = "";
            bool hasRight = false;

            for (int i = 0; i < Pens.Count; i++)
            {
                var pen = Pens[i];
                if (!pen.IsVisible) continue;

                double pMin = pen.MinValue;
                double pMax = pen.MaxValue;
                if (pen.AutoScale)
                {
                    pMin = pen.StatMin;
                    pMax = pen.StatMax;
                    if (pMax - pMin < 1.0)
                    {
                        double mid = (pMax + pMin) * 0.5;
                        pMin = mid - 0.5;
                        pMax = mid + 0.5;
                    }
                }

                if (string.Equals(pen.YAxisBinding, "Right", StringComparison.OrdinalIgnoreCase))
                {
                    hasRight = true;
                    if (pMin < rightMin) rightMin = pMin;
                    if (pMax > rightMax) rightMax = pMax;
                    if (string.IsNullOrEmpty(rightUnits) && !string.IsNullOrEmpty(pen.Units))
                        rightUnits = pen.Units;
                }
                else if (string.Equals(pen.YAxisBinding, "Left", StringComparison.OrdinalIgnoreCase))
                {
                    hasLeft = true;
                    if (pMin < leftMin) leftMin = pMin;
                    if (pMax > leftMax) leftMax = pMax;
                    if (string.IsNullOrEmpty(leftUnits) && !string.IsNullOrEmpty(pen.Units))
                        leftUnits = pen.Units;
                }
            }

            if (!hasLeft)
            {
                leftMin = TypedConfig.MinValue;
                leftMax = TypedConfig.MaxValue > TypedConfig.MinValue ? TypedConfig.MaxValue : 100.0;
            }
            if (Math.Abs(leftMax - leftMin) < 0.001) leftMax = leftMin + 1.0;

            if (!hasRight)
            {
                rightMin = 0.0;
                rightMax = 100.0;
            }
            if (Math.Abs(rightMax - rightMin) < 0.001) rightMax = rightMin + 1.0;

            LeftMinY = Math.Round(leftMin, 1);
            LeftMaxY = Math.Round(leftMax, 1);
            LeftMidY = Math.Round((leftMin + leftMax) / 2.0, 1);
            LeftUnits = leftUnits;

            RightMinY = Math.Round(rightMin, 1);
            RightMaxY = Math.Round(rightMax, 1);
            RightMidY = Math.Round((rightMin + rightMax) / 2.0, 1);
            RightUnits = rightUnits;
            HasRightAxis = hasRight;

            OnPropertyChanged(nameof(MinY));
            OnPropertyChanged(nameof(MaxY));
        }

        partial void OnSelectedSliceIndexChanged(int value)
        {
            UpdateCrosshairSlice();
        }

        partial void OnIsCrosshairActiveChanged(bool value)
        {
            if (!value)
            {
                CrosshairValues.Clear();
                CrosshairTimeText = string.Empty;
            }
            else
            {
                UpdateCrosshairSlice();
            }
        }

        private void UpdateCrosshairSlice()
        {
            if (SelectedSliceIndex < 0 || SelectedSliceIndex >= BufferCapacity)
            {
                CrosshairValues.Clear();
                CrosshairTimeText = string.Empty;
                return;
            }

            int remainingPoints = BufferCapacity - 1 - SelectedSliceIndex;
            double secFromEnd = (remainingPoints / (double)(BufferCapacity - 1)) * TypedConfig.TimeWindowSeconds;
            var sliceTime = DateTime.Now - TimeSpan.FromSeconds(secFromEnd);
            CrosshairTimeText = secFromEnd <= 0.2 ? "Сейчас" : $"-{secFromEnd:F1}с ({sliceTime:HH:mm:ss})";

            CrosshairValues.Clear();
            for (int i = 0; i < Pens.Count; i++)
            {
                var pen = Pens[i];
                if (!pen.IsVisible || SelectedSliceIndex >= pen.Buffer.Count) continue;

                double val = pen.Buffer[SelectedSliceIndex];
                CrosshairValues.Add(new CrosshairSliceItem
                {
                    PenName = pen.Name,
                    ColorHex = pen.ColorHex,
                    PenBrush = pen.PenBrush,
                    FormattedValue = pen.FormatValue(val),
                    Units = pen.Units
                });
            }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.PropertyName == nameof(SizeY))
            {
                OnPropertyChanged(nameof(IsLegendEffectiveVisible));
            }
        }

        public override void UpdateFromConfig(WidgetConfig newConfig)
        {
            base.UpdateFromConfig(newConfig);
            if (newConfig is RealTimeTrendConfig trendConfig)
            {
                trendConfig.EnsureDefaultPen();

                // Reconcile Pens in-place for Undo/Redo & zero-leak
                var incoming = trendConfig.Pens;
                for (int i = Pens.Count - 1; i >= 0; i--)
                {
                    var p = Pens[i];
                    if (!incoming.Any(c => c.Id == p.Id))
                    {
                        p.Dispose();
                        Pens.RemoveAt(i);
                    }
                }

                foreach (var penCfg in incoming)
                {
                    var existing = Pens.FirstOrDefault(p => p.Id == penCfg.Id);
                    if (existing != null)
                    {
                        existing.UpdateFromConfig(penCfg);
                    }
                    else
                    {
                        Pens.Add(new TrendPenViewModel(penCfg, DataService, BufferCapacity));
                    }
                }

                TypedConfig.Format = trendConfig.Format;
                TypedConfig.MinValue = trendConfig.MinValue;
                TypedConfig.MaxValue = trendConfig.MaxValue;
                TypedConfig.ScalingMode = trendConfig.ScalingMode;
                TypedConfig.TimeWindowSeconds = trendConfig.TimeWindowSeconds > 0 ? trendConfig.TimeWindowSeconds : 60;
                TypedConfig.ShowSetpoint = trendConfig.ShowSetpoint;
                TypedConfig.SetpointValue = trendConfig.SetpointValue;
                TypedConfig.ShowWarningLimits = trendConfig.ShowWarningLimits;
                TypedConfig.WarningHigh = trendConfig.WarningHigh;
                TypedConfig.WarningLow = trendConfig.WarningLow;
                TypedConfig.ShowAlarmLimits = trendConfig.ShowAlarmLimits;
                TypedConfig.AlarmHigh = trendConfig.AlarmHigh;
                TypedConfig.AlarmLow = trendConfig.AlarmLow;
                TypedConfig.ShowLegendTable = trendConfig.ShowLegendTable;
                TypedConfig.ShowLegendLatestValue = trendConfig.ShowLegendLatestValue;
                TypedConfig.ShowLegendMinMax = trendConfig.ShowLegendMinMax;
                TypedConfig.ShowLegendUnits = trendConfig.ShowLegendUnits;
                TypedConfig.EnableCrosshair = trendConfig.EnableCrosshair;

                if (_samplingTimer != null)
                {
                    _samplingTimer.Interval = TimeSpan.FromMilliseconds(GetSamplingIntervalMs());
                }

                UpdateAxesScales();
                UpdateTimeLabels();
                OnPropertyChanged(nameof(ScalingMode));
                OnPropertyChanged(nameof(Format));
                OnPropertyChanged(nameof(IsLegendEffectiveVisible));
                OnPropertyChanged(nameof(ShowSetpoint));
                OnPropertyChanged(nameof(SetpointValue));
                OnPropertyChanged(nameof(ShowWarningLimits));
                OnPropertyChanged(nameof(WarningHigh));
                OnPropertyChanged(nameof(WarningLow));
                OnPropertyChanged(nameof(ShowAlarmLimits));
                OnPropertyChanged(nameof(AlarmHigh));
                OnPropertyChanged(nameof(AlarmLow));
                OnPropertyChanged(nameof(EnableCrosshair));
                OnPropertyChanged(nameof(ShowLegendTable));
                OnPropertyChanged(nameof(ShowLegendLatestValue));
                OnPropertyChanged(nameof(ShowLegendMinMax));
                OnPropertyChanged(nameof(ShowLegendUnits));
            }
        }

        public override void Dispose()
        {
            if (_samplingTimer != null)
            {
                _samplingTimer.Stop();
                _samplingTimer = null;
            }

            foreach (var pen in Pens)
            {
                pen.Dispose();
            }
            Pens.Clear();

            base.Dispose();
        }
    }
}
