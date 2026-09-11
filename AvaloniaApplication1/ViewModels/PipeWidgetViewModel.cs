using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Threading;
using Avalonia;
using Avalonia.Media;

namespace AvaloniaApplication1.ViewModels
{
    public partial class PipeWidgetViewModel : WidgetViewModelBase
    {
        public PipeConfig TypedConfig => (PipeConfig)OriginalConfig;

        [ObservableProperty]
        private string _currentColor = "#555555";

        [ObservableProperty]
        private bool _isActive;

        [ObservableProperty]
        private bool _isEditingVertices;

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void ToggleEditingVertices()
        {
            IsEditingVertices = !IsEditingVertices;
        }

        public bool IsSuppressingNormalization { get; set; }

        public string PipePoints
        {
            get => TypedConfig.PipePoints;
            set
            {
                if (TypedConfig.PipePoints != value)
                {
                    TypedConfig.PipePoints = value;
                    OnPropertyChanged(nameof(PipePoints));
                    NormalizePointsAndSize();
                }
            }
        }

        public double Thickness
        {
            get => TypedConfig.Thickness == 0 ? 12.0 : TypedConfig.Thickness;
            set
            {
                if (TypedConfig.Thickness != value)
                {
                    TypedConfig.Thickness = value;
                    OnPropertyChanged(nameof(Thickness));
                }
            }
        }

        public string StartFitting
        {
            get => string.IsNullOrEmpty(TypedConfig.StartFitting) ? "None" : TypedConfig.StartFitting;
            set
            {
                if (TypedConfig.StartFitting != value)
                {
                    TypedConfig.StartFitting = value;
                    OnPropertyChanged(nameof(StartFitting));
                }
            }
        }

        public string EndFitting
        {
            get => string.IsNullOrEmpty(TypedConfig.EndFitting) ? "None" : TypedConfig.EndFitting;
            set
            {
                if (TypedConfig.EndFitting != value)
                {
                    TypedConfig.EndFitting = value;
                    OnPropertyChanged(nameof(EndFitting));
                }
            }
        }

        public Color ActiveColor
        {
            get
            {
                string colorStr = string.IsNullOrEmpty(TypedConfig.ActiveColor) ? "#00FFCC" : TypedConfig.ActiveColor;
                return Color.TryParse(colorStr, out var c) ? c : Colors.DodgerBlue;
            }
            set
            {
                string colorStr = value.ToString();
                if (TypedConfig.ActiveColor != colorStr)
                {
                    TypedConfig.ActiveColor = colorStr;
                    OnPropertyChanged(nameof(ActiveColor));
                    UpdateState();
                }
            }
        }

        public Color InactiveColor
        {
            get
            {
                string colorStr = string.IsNullOrEmpty(TypedConfig.InactiveColor) ? "#555555" : TypedConfig.InactiveColor;
                return Color.TryParse(colorStr, out var c) ? c : Colors.Gray;
            }
            set
            {
                string colorStr = value.ToString();
                if (TypedConfig.InactiveColor != colorStr)
                {
                    TypedConfig.InactiveColor = colorStr;
                    OnPropertyChanged(nameof(InactiveColor));
                    UpdateState();
                }
            }
        }

        public bool ShowFlanges
        {
            get => TypedConfig.ShowFlanges;
            set
            {
                if (TypedConfig.ShowFlanges != value)
                {
                    TypedConfig.ShowFlanges = value;
                    OnPropertyChanged(nameof(ShowFlanges));
                }
            }
        }

        public List<PipeWidgetViewModel> ConnectedPipes { get; } = new();

        public PipeWidgetViewModel(PipeConfig config, IDataCoreService dataService, IProjectContextService projectContext) 
            : base(config, dataService, projectContext)
        {
            NormalizePointsAndSize();
            UpdateState();
        }

        protected override void OnTagValueUpdated(object newValue)
        {
            UpdateState();
        }

        public List<Point> GetAbsoluteGridPoints()
        {
            var list = new List<Point>();
            if (string.IsNullOrEmpty(PipePoints)) return list;
            
            var segments = PipePoints.Split(new[] { ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var seg in segments)
            {
                var parts = seg.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
                {
                    list.Add(new Point(Col + x, Row + y));
                }
            }
            return list;
        }

        public void UpdateNetworkState(bool state)
        {
            var visited = new HashSet<PipeWidgetViewModel>();
            SetActiveStateFromNetwork(state, visited);
        }

        public void SetActiveStateFromNetwork(bool active, HashSet<PipeWidgetViewModel> visited)
        {
            if (visited.Contains(this)) return;
            visited.Add(this);

            IsActive = active;
            CurrentColor = IsActive ? ActiveColor.ToString() : InactiveColor.ToString();

            foreach (var neighbor in ConnectedPipes)
            {
                neighbor.SetActiveStateFromNetwork(active, visited);
            }
        }

        public void TriggerInitialUpdate()
        {
            UpdateState();
        }

        private void UpdateState()
        {
            if (Source == null || string.IsNullOrEmpty(Source.Address))
            {
                if (ConnectedPipes.Count == 0)
                {
                    CurrentColor = InactiveColor.ToString();
                    IsActive = false;
                }
                return;
            }

            var val = DataService.GetCurrentValue(Source.ConnId, Source.Address);
            bool active = false;
            if (val != null)
            {
                active = TagValueConverter.ToBool(val);
            }

            UpdateNetworkState(active);
        }

        private bool _isNormalizing;

        public void NormalizePointsAndSize()
        {
            if (_isNormalizing || IsSuppressingNormalization) return;
            
            var points = GetAbsoluteGridPoints();
            if (points.Count < 2) return;

            _isNormalizing = true;
            try
            {
                double minX = points.Min(p => p.X);
                double maxX = points.Max(p => p.X);
                double minY = points.Min(p => p.Y);
                double maxY = points.Max(p => p.Y);

                int newCol = (int)Math.Floor(minX);
                int newRow = (int)Math.Floor(minY);
                int newSizeX = Math.Max(1, (int)Math.Ceiling(maxX - newCol));
                int newSizeY = Math.Max(1, (int)Math.Ceiling(maxY - newRow));

                var relativePoints = points.Select(p => new Point(p.X - newCol, p.Y - newRow)).ToList();
                string newPipePoints = string.Join(";", relativePoints.Select(p => string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", p.X, p.Y)));

                if (Col != newCol) { Col = newCol; OriginalConfig.Position.Col = newCol; }
                if (Row != newRow) { Row = newRow; OriginalConfig.Position.Row = newRow; }
                if (SizeX != newSizeX) { SizeX = newSizeX; OriginalConfig.Position.SizeX = newSizeX; }
                if (SizeY != newSizeY) { SizeY = newSizeY; OriginalConfig.Position.SizeY = newSizeY; }

                if (TypedConfig.PipePoints != newPipePoints)
                {
                    TypedConfig.PipePoints = newPipePoints;
                    OnPropertyChanged(nameof(PipePoints));
                }
            }
            finally
            {
                _isNormalizing = false;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
