using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels
{
    public partial class DashboardViewModel : ViewModelBase
    {
        private readonly IMockDataService _mockDataService;
        private readonly IDialogService? _dialogService;
        private readonly HmiConfiguration _config;
        private readonly DashboardConfig _dashboardConfig;

        public IProjectContextService ProjectContext { get; }
        
        public ObservableCollection<WidgetViewModelBase> Widgets { get; } = new();

        public double CellWidth { get; }
        public double CellHeight { get; }

        [ObservableProperty]
        private double _zoomScale = 1.0;

        public DashboardViewModel(
            DashboardConfig dashboardConfig, 
            IMockDataService mockDataService, 
            IProjectContextService projectContext,
            HmiConfiguration config,
            IDialogService? dialogService)
        {
            _mockDataService = mockDataService;
            _dialogService = dialogService;
            _config = config;
            _dashboardConfig = dashboardConfig;
            ProjectContext = projectContext;

            CellWidth = dashboardConfig.CellSize == 0 ? 160 : dashboardConfig.CellSize;
            CellHeight = dashboardConfig.CellSize == 0 ? 160 : dashboardConfig.CellSize;

            LoadWidgets(dashboardConfig);
            ResolvePipeConnections();
        }

        private void LoadWidgets(DashboardConfig config)
        {
            foreach (var widgetConfig in config.Widgets)
            {
                var vm = CreateWidgetViewModel(widgetConfig);
                if (vm != null)
                {
                    vm.PropertyChanged += OnWidgetPropertyChanged;
                    Widgets.Add(vm);
                }
            }
        }

        private WidgetViewModelBase? CreateWidgetViewModel(WidgetConfig widgetConfig)
        {
            return widgetConfig.Type switch
            {
                "ValueDisplay" => new ValueDisplayViewModel(widgetConfig, _mockDataService, ProjectContext),
                "PilotLight" => new PilotLightViewModel(widgetConfig, _mockDataService, ProjectContext),
                "ContainerButton" => new ContainerButtonViewModel(widgetConfig, _mockDataService, ProjectContext),
                "CommandButton" => new CommandButtonViewModel(widgetConfig, _mockDataService, ProjectContext),
                "Slider" => new SliderViewModel(widgetConfig, _mockDataService, ProjectContext),
                "SetValue" => new SetValueViewModel(widgetConfig, _mockDataService, ProjectContext),
                "RealTimeTrend" => new RealTimeTrendViewModel(widgetConfig, _mockDataService, ProjectContext),
                "Pipe" => new PipeWidgetViewModel(widgetConfig, _mockDataService, ProjectContext),
                "Valve" => new ValveWidgetViewModel(widgetConfig, _mockDataService, ProjectContext),
                "Tank" => new TankWidgetViewModel(widgetConfig, _mockDataService, ProjectContext),
                "Pump" => new PumpWidgetViewModel(widgetConfig, _mockDataService, ProjectContext),
                _ => new WidgetViewModelBase(widgetConfig, _mockDataService, ProjectContext)
            };
        }

        // ===== Design Mode Commands =====

        [RelayCommand]
        private void BringToFront(WidgetViewModelBase? widget)
        {
            if (widget == null) return;
            int index = Widgets.IndexOf(widget);
            if (index >= 0 && index < Widgets.Count - 1)
            {
                Widgets.Move(index, Widgets.Count - 1);
                
                var config = _dashboardConfig.Widgets[index];
                _dashboardConfig.Widgets.RemoveAt(index);
                _dashboardConfig.Widgets.Add(config);
            }
        }

        [RelayCommand]
        private void BringForward(WidgetViewModelBase? widget)
        {
            if (widget == null) return;
            int index = Widgets.IndexOf(widget);
            if (index >= 0 && index < Widgets.Count - 1)
            {
                Widgets.Move(index, index + 1);
                
                var config = _dashboardConfig.Widgets[index];
                _dashboardConfig.Widgets.RemoveAt(index);
                _dashboardConfig.Widgets.Insert(index + 1, config);
            }
        }

        [RelayCommand]
        private void SendBackward(WidgetViewModelBase? widget)
        {
            if (widget == null) return;
            int index = Widgets.IndexOf(widget);
            if (index > 0)
            {
                Widgets.Move(index, index - 1);
                
                var config = _dashboardConfig.Widgets[index];
                _dashboardConfig.Widgets.RemoveAt(index);
                _dashboardConfig.Widgets.Insert(index - 1, config);
            }
        }

        [RelayCommand]
        private void SendToBack(WidgetViewModelBase? widget)
        {
            if (widget == null) return;
            int index = Widgets.IndexOf(widget);
            if (index > 0)
            {
                Widgets.Move(index, 0);
                
                var config = _dashboardConfig.Widgets[index];
                _dashboardConfig.Widgets.RemoveAt(index);
                _dashboardConfig.Widgets.Insert(0, config);
            }
        }

        [RelayCommand]
        private async Task AddWidgetAsync()
        {
            if (_dialogService == null) return;

            var result = await _dialogService.ShowWidgetEditorAsync(null, _config.Connections);
            if (result != null)
            {
                // Add to config model
                _dashboardConfig.Widgets.Add(result);
                
                // Create and add VM
                var vm = CreateWidgetViewModel(result);
                if (vm != null)
                {
                    vm.PropertyChanged += OnWidgetPropertyChanged;
                    Widgets.Add(vm);
                    ResolvePipeConnections();
                }
            }
        }

        [RelayCommand]
        private void RemoveWidget(WidgetViewModelBase? widget)
        {
            if (widget == null) return;

            // Remove from config model
            _dashboardConfig.Widgets.Remove(widget.OriginalConfig);
            
            // Remove VM and dispose
            widget.PropertyChanged -= OnWidgetPropertyChanged;
            widget.Dispose();
            Widgets.Remove(widget);
            ResolvePipeConnections();
        }

        [RelayCommand]
        private void DuplicateWidget(WidgetViewModelBase? widget)
        {
            if (widget == null) return;

            var sourceConfig = widget.OriginalConfig;
            var json = System.Text.Json.JsonSerializer.Serialize(sourceConfig);
            var clonedConfig = System.Text.Json.JsonSerializer.Deserialize<WidgetConfig>(json);

            if (clonedConfig != null)
            {
                clonedConfig.Position.Row += 2;
                clonedConfig.Position.Col += 2;

                _dashboardConfig.Widgets.Add(clonedConfig);

                var vm = CreateWidgetViewModel(clonedConfig);
                if (vm != null)
                {
                    vm.PropertyChanged += OnWidgetPropertyChanged;
                    Widgets.Add(vm);
                    ResolvePipeConnections();
                }
            }
        }

        [RelayCommand]
        private async Task EditWidgetAsync(WidgetViewModelBase? widget)
        {
            if (widget == null || _dialogService == null) return;

            var result = await _dialogService.ShowWidgetEditorAsync(widget.OriginalConfig, _config.Connections);
            if (result != null)
            {
                // Find and replace in config model
                var index = _dashboardConfig.Widgets.IndexOf(widget.OriginalConfig);
                if (index >= 0)
                    _dashboardConfig.Widgets[index] = result;

                // Replace VM
                var vmIndex = Widgets.IndexOf(widget);
                widget.PropertyChanged -= OnWidgetPropertyChanged;
                widget.Dispose();
                
                var newVm = CreateWidgetViewModel(result);
                if (newVm != null && vmIndex >= 0)
                {
                    newVm.PropertyChanged += OnWidgetPropertyChanged;
                    Widgets[vmIndex] = newVm;
                    ResolvePipeConnections();
                }
            }
        }

        [RelayCommand]
        private void ResetZoom()
        {
            ZoomScale = 1.0;
        }

        private void OnWidgetPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WidgetViewModelBase.Col) || 
                e.PropertyName == nameof(WidgetViewModelBase.Row) || 
                e.PropertyName == "PipePoints")
            {
                ResolvePipeConnections();
            }
        }

        private void ResolvePipeConnections()
        {
            var pipes = Widgets.OfType<PipeWidgetViewModel>().ToList();
            
            // Clear old links
            foreach (var pipe in pipes)
            {
                pipe.ConnectedPipes.Clear();
            }

            // Find connections by coordinates
            for (int i = 0; i < pipes.Count; i++)
            {
                var pipeA = pipes[i];
                var pointsA = pipeA.GetAbsoluteGridPoints();
                if (pointsA.Count < 2) continue;

                var startA = pointsA.First();
                var endA = pointsA.Last();

                for (int j = i + 1; j < pipes.Count; j++)
                {
                    var pipeB = pipes[j];
                    var pointsB = pipeB.GetAbsoluteGridPoints();
                    if (pointsB.Count < 2) continue;

                    var startB = pointsB.First();
                    var endB = pointsB.Last();

                    // Check closeness on grid
                    if (ArePointsClose(startA, startB) || ArePointsClose(startA, endB) ||
                        ArePointsClose(endA, startB) || ArePointsClose(endA, endB))
                    {
                        pipeA.ConnectedPipes.Add(pipeB);
                        pipeB.ConnectedPipes.Add(pipeA);
                    }
                }
            }

            // Reset active state of all non-source pipes
            foreach (var pipe in pipes)
            {
                if (pipe.Source == null || string.IsNullOrEmpty(pipe.Source.Address))
                {
                    pipe.IsActive = false;
                    pipe.CurrentColor = pipe.InactiveColor.ToString();
                }
            }

            // Trigger initial network update from sources
            foreach (var pipe in pipes)
            {
                if (pipe.Source != null && !string.IsNullOrEmpty(pipe.Source.Address))
                {
                    pipe.TriggerInitialUpdate();
                }
            }
        }

        private bool ArePointsClose(Avalonia.Point p1, Avalonia.Point p2)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2)) <= 1.1;
        }
    }
}