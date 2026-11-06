using System;
using System.Globalization;
using AvaloniaApplication1.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels
{
    /// <summary>
    /// Interactive view model representing a single tag row in the Tag Manager table.
    /// Provides real-time telemetry updates, quality indicators, and force override actions.
    /// </summary>
    public partial class TagItemViewModel : ObservableObject
    {
        public TagDefinition Definition { get; }

        public string TagId => Definition.TagId;

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _connId;

        [ObservableProperty]
        private string _address;

        [ObservableProperty]
        private string _dataType;

        [ObservableProperty]
        private string? _unit;

        [ObservableProperty]
        private double _scaleA;

        [ObservableProperty]
        private double _scaleB;

        [ObservableProperty]
        private string _scalingDisplay = "1.0*X";

        [ObservableProperty]
        private double _deadband;

        [ObservableProperty]
        private TimeSpan _staleTimeout;

        [ObservableProperty]
        private string? _description;

        // --- Live Telemetry Properties ---
        [ObservableProperty]
        private string _liveValue = "---";

        [ObservableProperty]
        private object? _rawValue;

        [ObservableProperty]
        private TagQuality _quality = TagQuality.Uninitialized;

        [ObservableProperty]
        private string _qualityText = "Uninitialized";

        [ObservableProperty]
        private string _qualityBadgeBackground = "#2D2D30";

        [ObservableProperty]
        private string _qualityForeground = "#888888";

        [ObservableProperty]
        private string _qualityGlyph = "?";

        [ObservableProperty]
        private string _valueBrush = "#888888";

        [ObservableProperty]
        private string _timestampText = "--:--:--";

        [ObservableProperty]
        private bool _isOverridden;

        // Callback hooks for parent TagManagerViewModel
        public Action<TagItemViewModel>? RequestForceOverride { get; set; }
        public Action<TagItemViewModel>? RequestClearOverride { get; set; }

        public TagItemViewModel(TagDefinition definition)
        {
            Definition = definition;
            _name = definition.Name;
            _connId = definition.ConnId;
            _address = definition.Address;
            _dataType = definition.DataType;
            _unit = definition.Unit;
            _scaleA = definition.ScaleA;
            _scaleB = definition.ScaleB;
            _scalingDisplay = definition.ScalingDisplay;
            _deadband = definition.Deadband;
            _staleTimeout = definition.StaleTimeout;
            _description = definition.Description;

            UpdateQualityPresentation(TagQuality.Uninitialized, false);
        }

        public void RefreshFromDefinition()
        {
            Name = Definition.Name;
            ConnId = Definition.ConnId;
            Address = Definition.Address;
            DataType = Definition.DataType;
            Unit = Definition.Unit;
            ScaleA = Definition.ScaleA;
            ScaleB = Definition.ScaleB;
            ScalingDisplay = Definition.ScalingDisplay;
            Deadband = Definition.Deadband;
            StaleTimeout = Definition.StaleTimeout;
            Description = Definition.Description;
        }

        public void ApplySnapshot(TagSnapshot snapshot)
        {
            RawValue = snapshot.Value;
            Quality = snapshot.Quality;
            IsOverridden = snapshot.IsOverridden || snapshot.Quality == TagQuality.Overridden;
            TimestampText = snapshot.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");

            if (snapshot.Quality == TagQuality.CommFailure || snapshot.Quality == TagQuality.Timeout)
            {
                LiveValue = "---";
            }
            else if (snapshot.Value != null)
            {
                if (snapshot.Value is bool b)
                {
                    LiveValue = b ? "TRUE (1)" : "FALSE (0)";
                }
                else if (snapshot.Value is double or float or int or short or ushort or uint or long or ulong or decimal)
                {
                    try
                    {
                        double dVal = Convert.ToDouble(snapshot.Value, CultureInfo.InvariantCulture);
                        var formatted = Math.Abs(dVal) >= 1000 
                            ? dVal.ToString("N1", CultureInfo.InvariantCulture) 
                            : dVal.ToString("0.##", CultureInfo.InvariantCulture);
                        LiveValue = string.IsNullOrWhiteSpace(Unit) ? formatted : $"{formatted} {Unit}";
                    }
                    catch
                    {
                        LiveValue = snapshot.Value.ToString() ?? "---";
                    }
                }
                else
                {
                    LiveValue = snapshot.Value.ToString() ?? "---";
                }
            }
            else
            {
                LiveValue = "---";
            }

            UpdateQualityPresentation(snapshot.Quality, IsOverridden);
        }

        private void UpdateQualityPresentation(TagQuality quality, bool isOverridden)
        {
            if (isOverridden)
            {
                QualityText = "[M] Overridden";
                QualityGlyph = "⚡";
                QualityBadgeBackground = "#381E48"; // Deep Purple
                QualityForeground = "#E040FB";      // Neon Purple / Fuchsia
                ValueBrush = "#E040FB";
                return;
            }

            switch (quality)
            {
                case TagQuality.Good:
                    QualityText = "Good";
                    QualityGlyph = "✓";
                    QualityBadgeBackground = "#153E1B"; // Deep Green
                    QualityForeground = "#00E676";      // Neon Green
                    ValueBrush = "#00E5FF";             // Neon Cyan
                    break;

                case TagQuality.Stale:
                    QualityText = "Stale";
                    QualityGlyph = "⏳";
                    QualityBadgeBackground = "#4D2C00"; // Deep Amber
                    QualityForeground = "#FFB300";      // Amber
                    ValueBrush = "#FFB300";
                    break;

                case TagQuality.Timeout:
                    QualityText = "Timeout";
                    QualityGlyph = "⚠️";
                    QualityBadgeBackground = "#4A1212"; // Deep Red
                    QualityForeground = "#FF5252";      // Light Red
                    ValueBrush = "#858585";
                    break;

                case TagQuality.CommFailure:
                    QualityText = "CommFail";
                    QualityGlyph = "❌";
                    QualityBadgeBackground = "#4A1212";
                    QualityForeground = "#FF5252";
                    ValueBrush = "#858585";
                    break;

                case TagQuality.Bad:
                case TagQuality.OutOfRange:
                    QualityText = quality.ToString();
                    QualityGlyph = "⚠️";
                    QualityBadgeBackground = "#4A1212";
                    QualityForeground = "#FF5252";
                    ValueBrush = "#FF5252";
                    break;

                case TagQuality.Uninitialized:
                default:
                    QualityText = "No Telemetry";
                    QualityGlyph = "•";
                    QualityBadgeBackground = "#252526";
                    QualityForeground = "#858585";
                    ValueBrush = "#858585";
                    break;
            }
        }

        [RelayCommand]
        private void ForceOverride()
        {
            RequestForceOverride?.Invoke(this);
        }

        [RelayCommand]
        private void ClearOverride()
        {
            RequestClearOverride?.Invoke(this);
        }
    }
}
