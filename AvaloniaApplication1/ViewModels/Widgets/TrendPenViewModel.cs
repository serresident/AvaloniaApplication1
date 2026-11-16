using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Media;
using AvaloniaApplication1.Controls.Charting;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Models.Common;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using ReactiveUI;

namespace AvaloniaApplication1.ViewModels.Widgets
{
    public partial class TrendPenViewModel : ObservableObject, ITrendPenRenderData, IDisposable
    {
        private readonly CompositeDisposable _disposables = new();
        private readonly SerialDisposable _subscriptionDisposable = new();
        private readonly RingBuffer<double> _buffer;
        private double _latestRawValue;

        public TrendPenConfig Config { get; private set; }
        public IDataCoreService DataService { get; }

        [ObservableProperty]
        private string _name = "Перо";

        [ObservableProperty]
        private string _colorHex = "#00FFCC";

        [ObservableProperty]
        private IBrush _penBrush = Brushes.Aqua;

        [ObservableProperty]
        private double _thickness = 2.0;

        [ObservableProperty]
        private string _strokeStyle = "Solid";

        [ObservableProperty]
        private string _interpolationMode = "Linear";

        [ObservableProperty]
        private string _yAxisBinding = "Left";

        [ObservableProperty]
        private double _minValue = 0.0;

        [ObservableProperty]
        private double _maxValue = 100.0;

        [ObservableProperty]
        private bool _autoScale = false;

        [ObservableProperty]
        private string _units = string.Empty;

        [ObservableProperty]
        private string _format = "{0:F1}";

        [ObservableProperty]
        private bool _isVisible = true;

        [ObservableProperty]
        private double _latestValue;

        [ObservableProperty]
        private double _statMin;

        [ObservableProperty]
        private double _statMax;

        [ObservableProperty]
        private string _latestFormatted = "---";

        [ObservableProperty]
        private string _minFormatted = "---";

        [ObservableProperty]
        private string _maxFormatted = "---";

        public RingBuffer<double> Buffer => _buffer;
        public IReadOnlyList<double> Values => _buffer;

        public Guid Id => Config.Id;

        public TrendPenViewModel(TrendPenConfig config, IDataCoreService dataService, int bufferCapacity = 120)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            DataService = dataService ?? throw new ArgumentNullException(nameof(dataService));

            _buffer = new RingBuffer<double>(bufferCapacity > 0 ? bufferCapacity : 120);
            _disposables.Add(_subscriptionDisposable);

            ApplyConfig(config);
            InitializeInitialValue();
            SubscribeToDataService();
        }

        private void ApplyConfig(TrendPenConfig config)
        {
            Config = config;
            Name = config.Name;
            ColorHex = config.ColorHex;
            PenBrush = CreateBrush(config.ColorHex);
            Thickness = config.Thickness;
            StrokeStyle = config.StrokeStyle;
            InterpolationMode = config.InterpolationMode;
            YAxisBinding = config.YAxisBinding;
            MinValue = config.MinValue;
            MaxValue = config.MaxValue > config.MinValue ? config.MaxValue : config.MinValue + 1.0;
            AutoScale = config.AutoScale;
            Units = config.Units ?? string.Empty;
            Format = string.IsNullOrWhiteSpace(config.Format) ? "{0:F1}" : config.Format;
            IsVisible = config.IsVisible;
        }

        private static IBrush CreateBrush(string hex)
        {
            try
            {
                return new SolidColorBrush(Color.Parse(hex));
            }
            catch
            {
                return Brushes.Aqua;
            }
        }

        private void InitializeInitialValue()
        {
            double initialVal = 0.0;
            string? connId = Config.Source?.ConnId;
            string? address = Config.Source?.Address;
            string? tagId = Config.Source?.TagId;

            if (!string.IsNullOrEmpty(tagId) && DataService.TagManager != null)
            {
                var snap = DataService.GetTagSnapshot(tagId);
                if (snap.Value != null && TryConvertDouble(snap.Value, out double sVal))
                {
                    initialVal = sVal;
                }
                else
                {
                    var tagDef = DataService.TagManager.GetTagDefinition(tagId);
                    if (tagDef != null)
                    {
                        if (string.IsNullOrEmpty(connId)) connId = tagDef.ConnId;
                        if (string.IsNullOrEmpty(address)) address = tagDef.Address;
                    }
                }
            }

            if (initialVal == 0.0 && !string.IsNullOrEmpty(connId) && !string.IsNullOrEmpty(address))
            {
                var val = DataService.GetCurrentValue(connId, address);
                if (val != null && TryConvertDouble(val, out double dVal))
                {
                    initialVal = dVal;
                }
            }

            _latestRawValue = initialVal;
            LatestValue = initialVal;
            StatMin = initialVal;
            StatMax = initialVal;

            _buffer.PreFill(initialVal);
            UpdateStatistics();
        }

        private void SubscribeToDataService()
        {
            if (Config.Source == null) return;

            string? connId = Config.Source.ConnId;
            string? address = Config.Source.Address;
            string? tagId = Config.Source.TagId;

            if ((string.IsNullOrEmpty(connId) || string.IsNullOrEmpty(address)) && !string.IsNullOrEmpty(tagId) && DataService.TagManager != null)
            {
                var tagDef = DataService.TagManager.GetTagDefinition(tagId);
                if (tagDef != null)
                {
                    connId = tagDef.ConnId;
                    address = tagDef.Address;
                }
            }

            var composite = new CompositeDisposable();

            if (!string.IsNullOrEmpty(connId) && !string.IsNullOrEmpty(address))
            {
                var tagSub = DataService.TagUpdates
                    .Where(t => string.Equals(t.ConnId, connId, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(t.Address, address, StringComparison.OrdinalIgnoreCase))
                    .Subscribe(tag =>
                    {
                        if (TryConvertDouble(tag.Value, out double val))
                        {
                            _latestRawValue = val;
                        }
                    });
                composite.Add(tagSub);
            }

            if (!string.IsNullOrEmpty(tagId))
            {
                var snapSub = DataService.SnapshotUpdates
                    .Where(s => string.Equals(s.TagId, tagId, StringComparison.OrdinalIgnoreCase))
                    .Subscribe(s =>
                    {
                        if (s.Value != null && TryConvertDouble(s.Value, out double val))
                        {
                            _latestRawValue = val;
                        }
                    });
                composite.Add(snapSub);
            }

            _subscriptionDisposable.Disposable = composite;
        }

        public void PushSample()
        {
            double val = _latestRawValue;
            _buffer.Push(val);
            LatestValue = val;
            UpdateStatistics();
        }

        public void UpdateStatistics()
        {
            if (_buffer.Count == 0) return;

            double min = double.MaxValue;
            double max = double.MinValue;
            for (int i = 0; i < _buffer.Count; i++)
            {
                double v = _buffer[i];
                if (v < min) min = v;
                if (v > max) max = v;
            }

            StatMin = min;
            StatMax = max;
            LatestFormatted = FormatValue(LatestValue);
            MinFormatted = FormatValue(StatMin);
            MaxFormatted = FormatValue(StatMax);
        }

        public string FormatValue(double value)
        {
            try
            {
                return string.Format(CultureInfo.InvariantCulture, Format, value);
            }
            catch
            {
                return value.ToString("F1", CultureInfo.InvariantCulture);
            }
        }

        public void UpdateFromConfig(TrendPenConfig newConfig)
        {
            bool sourceChanged = Config.Source?.ConnId != newConfig.Source?.ConnId ||
                                 Config.Source?.Address != newConfig.Source?.Address;

            ApplyConfig(newConfig);

            if (sourceChanged)
            {
                SubscribeToDataService();
            }
            UpdateStatistics();
        }

        public static bool TryConvertDouble(object? valObj, out double result)
        {
            if (valObj == null) { result = 0; return false; }
            if (valObj is double d) { result = d; return true; }
            if (valObj is float f) { result = f; return true; }
            if (valObj is int i) { result = i; return true; }
            if (valObj is short s) { result = s; return true; }
            if (valObj is ushort us) { result = us; return true; }
            if (valObj is uint ui) { result = ui; return true; }
            if (valObj is long l) { result = l; return true; }
            if (valObj is bool b) { result = b ? 1.0 : 0.0; return true; }
            return double.TryParse(valObj.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
