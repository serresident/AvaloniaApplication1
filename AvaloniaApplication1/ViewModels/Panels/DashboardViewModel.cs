using AvaloniaApplication1.Models.History;
using AvaloniaApplication1.Services.History;
using AvaloniaApplication1.ViewModels.Core;
using AvaloniaApplication1.ViewModels.Widgets;
using AvaloniaApplication1.ViewModels.Popups;
using AvaloniaApplication1.ViewModels.Windows.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using AvaloniaApplication1.Services.Piping;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels.Panels
{
    public partial class DashboardViewModel : ViewModelBase, IDisposable
    {
        private readonly IDataCoreService _mockDataService;
        private readonly IDialogService? _dialogService;
        private readonly IWidgetFactory _widgetFactory;
        private readonly HmiConfiguration _config;
        private readonly DashboardConfig _dashboardConfig;
        private readonly IHistoryService _historyService;

        public IHistoryService HistoryService => _historyService;
        public HistoryPanelViewModel HistoryPanel { get; }

        private bool _isReconciling;
        public bool IsReconciling => _isReconciling;

        public IProjectContextService ProjectContext { get; }
        
        public ObservableCollection<WidgetViewModelBase> Widgets { get; } = new();

        [ObservableProperty]
        private double _cellWidth = 40;

        [ObservableProperty]
        private double _cellHeight = 40;

        [ObservableProperty]
        private double _zoomScale = 1.0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMultiSelection))]
        private int _selectedWidgetsCount;

        [ObservableProperty]
        private string _selectedWidgetsSummary = string.Empty;

        [ObservableProperty]
        private bool _isMultiSelectMode;

        [ObservableProperty]
        private bool _isRubberBandingEnabled = true;

        [ObservableProperty]
        private bool _canAssembleCollinear;

        [ObservableProperty]
        private bool _isHistoryPanelOpen;

        [ObservableProperty]
        private string _historyStepsBadgeText = "История (1)";

        public bool CanUndo => _historyService.CanUndo;
        public bool CanRedo => _historyService.CanRedo;

        public bool HasMultiSelection => SelectedWidgetsCount > 1;
        public bool CanGroup => SelectedWidgetsCount > 1;
        public bool CanUngroup => Widgets.Any(w => w.IsSelected && !string.IsNullOrEmpty(w.GroupId));

        public event Action<double, double>? OnGridOrScaleChanged;

        public void AdjustZoom(double delta)
        {
            ZoomScale = Math.Clamp(ZoomScale + delta, 0.2, 3.0);
            _dashboardConfig.ZoomScale = ZoomScale;
            OnGridOrScaleChanged?.Invoke(CellWidth, ZoomScale);
        }

        public void UpdateGridAndScale(double cellSize, double zoomScale)
        {
            CellWidth = cellSize;
            CellHeight = cellSize;
            ZoomScale = zoomScale;
            _dashboardConfig.CellSize = (int)Math.Round(cellSize);
            _dashboardConfig.ZoomScale = zoomScale;
            OnGridOrScaleChanged?.Invoke(cellSize, zoomScale);
        }

        public DashboardViewModel(
            DashboardConfig dashboardConfig, 
            IDataCoreService mockDataService, 
            IProjectContextService projectContext,
            HmiConfiguration config,
            IDialogService? dialogService,
            IWidgetFactory widgetFactory,
            IHistoryService? historyService = null)
        {
            _mockDataService = mockDataService;
            _dialogService = dialogService;
            _widgetFactory = widgetFactory;
            _config = config;
            _dashboardConfig = dashboardConfig;
            ProjectContext = projectContext;

            CellWidth = dashboardConfig.CellSize <= 0 ? 40 : dashboardConfig.CellSize;
            CellHeight = dashboardConfig.CellSize <= 0 ? 40 : dashboardConfig.CellSize;
            ZoomScale = dashboardConfig.ZoomScale <= 0 ? 1.0 : dashboardConfig.ZoomScale;

            _historyService = historyService ?? new HistoryService(projectContext);
            _historyService.SetSnapshotProvider(() => CreateSnapshot());
            _historyService.StepRestored += (s, step) => RestoreSnapshot(step.Snapshot);
            _historyService.HistoryChanged += (s, ev) => UpdateHistoryState();
            HistoryPanel = new HistoryPanelViewModel(_historyService, this);

            LoadWidgets(dashboardConfig);
            ResolvePipeConnections();

            _historyService.Initialize(CreateSnapshot(), "Исходное состояние", "Home");
            UpdateHistoryState();
        }

        private void UpdateHistoryState()
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            HistoryStepsBadgeText = $"История ({_historyService.Steps.Count})";
            OnPropertyChanged(nameof(HistoryStepsBadgeText));
        }

        public DashboardConfig CreateSnapshot()
        {
            var snapshot = new DashboardConfig
            {
                CellSize = (int)Math.Round(CellWidth),
                ZoomScale = ZoomScale,
                Widgets = new List<WidgetConfig>()
            };

            foreach (var vm in Widgets)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(vm.OriginalConfig);
                var cloned = System.Text.Json.JsonSerializer.Deserialize<WidgetConfig>(json);
                if (cloned != null)
                {
                    cloned.Position.Col = vm.Col;
                    cloned.Position.Row = vm.Row;
                    cloned.Position.SizeX = vm.SizeX;
                    cloned.Position.SizeY = vm.SizeY;
                    if (vm is PipeWidgetViewModel pvm && cloned is PipeConfig pc)
                    {
                        pc.PipePoints = pvm.PipePoints;
                    }
                    snapshot.Widgets.Add(cloned);
                }
            }

            return snapshot;
        }

        public void RestoreSnapshot(DashboardConfig snapshot)
        {
            if (snapshot == null) return;

            _isReconciling = true;
            try
            {
                UpdateGridAndScale(snapshot.CellSize, snapshot.ZoomScale);

                var targetConfigs = new Dictionary<Guid, WidgetConfig>();
                foreach (var w in snapshot.Widgets)
                {
                    if (w.Id == Guid.Empty)
                    {
                        w.Id = Guid.NewGuid();
                    }
                    targetConfigs[w.Id] = w;
                }

                // 1. Remove deleted widgets (not in snapshot)
                var existingVms = Widgets.ToList();
                foreach (var vm in existingVms)
                {
                    if (!targetConfigs.ContainsKey(vm.Id))
                    {
                        vm.PropertyChanged -= OnWidgetPropertyChanged;
                        vm.Dispose();
                        Widgets.Remove(vm);
                        _dashboardConfig.Widgets.Remove(vm.OriginalConfig);
                    }
                }

                // 2. In-place update for existing widgets (no VM drop, no subscription drop)
                foreach (var vm in Widgets.ToList())
                {
                    if (targetConfigs.TryGetValue(vm.Id, out var targetConfig))
                    {
                        vm.UpdateFromConfig(targetConfig);
                    }
                }

                // 3. Add newly restored widgets
                var currentIds = Widgets.Select(w => w.Id).ToHashSet();
                foreach (var cfg in snapshot.Widgets)
                {
                    if (!currentIds.Contains(cfg.Id))
                    {
                        if (!_dashboardConfig.Widgets.Contains(cfg))
                        {
                            _dashboardConfig.Widgets.Add(cfg);
                        }
                        var newVm = CreateWidgetViewModel(cfg);
                        if (newVm != null)
                        {
                            newVm.PropertyChanged += OnWidgetPropertyChanged;
                            Widgets.Add(newVm);
                        }
                    }
                }

                // 4. Reconcile collection order to match snapshot exact sequence
                for (int i = 0; i < snapshot.Widgets.Count; i++)
                {
                    var targetId = snapshot.Widgets[i].Id;
                    int currentIndex = -1;
                    for (int j = i; j < Widgets.Count; j++)
                    {
                        if (Widgets[j].Id == targetId)
                        {
                            currentIndex = j;
                            break;
                        }
                    }
                    if (currentIndex >= 0 && currentIndex != i)
                    {
                        Widgets.Move(currentIndex, i);
                    }
                }
                _dashboardConfig.Widgets.Clear();
                foreach (var vm in Widgets)
                {
                    _dashboardConfig.Widgets.Add(vm.OriginalConfig);
                }

                ResolvePipeConnections();
                UpdateSelectedWidgetsInfo();
            }
            finally
            {
                _isReconciling = false;
            }
        }

        public void RecordStep(string title, string iconKey)
        {
            if (_isReconciling) return;
            if (ProjectContext != null && !ProjectContext.IsDesignMode) return;

            var snapshot = CreateSnapshot();
            _historyService.RecordStep(title, iconKey, snapshot);
        }

        [RelayCommand]
        public void Undo()
        {
            if (!CanUndo) return;
            if (ProjectContext != null && !ProjectContext.IsDesignMode) return;
            _historyService.Undo();
        }

        [RelayCommand]
        public void Redo()
        {
            if (!CanRedo) return;
            if (ProjectContext != null && !ProjectContext.IsDesignMode) return;
            _historyService.Redo();
        }

        [RelayCommand]
        public void JumpToStep(HistoryStep? step)
        {
            if (step == null) return;
            if (ProjectContext != null && !ProjectContext.IsDesignMode) return;
            _historyService.JumpToStep(step);
        }

        [RelayCommand]
        public void ToggleHistoryPanel()
        {
            IsHistoryPanelOpen = !IsHistoryPanelOpen;
        }

        [RelayCommand]
        public void CloseHistoryPanel()
        {
            IsHistoryPanelOpen = false;
        }

        [RelayCommand]
        public void ClearHistory()
        {
            _historyService.Clear();
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
            UpdateSelectedWidgetsInfo();
        }

        public WidgetViewModelBase? CreateWidgetViewModel(WidgetConfig widgetConfig)
        {
            return _widgetFactory.CreateWidgetViewModel(widgetConfig);
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
                RecordStep($"На передний план: {widget.Title}", "Order");
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
                RecordStep($"Переместить вперед: {widget.Title}", "Order");
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
                RecordStep($"Переместить назад: {widget.Title}", "Order");
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
                RecordStep($"На задний план: {widget.Title}", "Order");
            }
        }

        [RelayCommand]
        private void AutoRoutePipe(PipeWidgetViewModel? pipe)
        {
            if (pipe == null) return;
            var absPoints = pipe.GetAbsoluteGridPoints();
            if (absPoints.Count < 2) return;

            var start = absPoints.First();
            var end = absPoints.Last();

            // Препятствия: остальные аппараты мнемосхемы
            var obstacles = Widgets
                .Where(w => w != pipe && !(w is PipeWidgetViewModel))
                .Select(w => new Avalonia.Rect(w.Col, w.Row, w.SizeX, w.SizeY));

            var routed = PipeAutoRouter.FindOrthogonalRoute(start, end, obstacles);
            if (routed.Count >= 2)
            {
                pipe.PipePoints = PipeAutoRouter.PointsToPipeString(routed);
                pipe.NormalizePointsAndSize();
                ResolvePipeConnections();
                RecordStep($"Автотрассировка трубы: {pipe.Title}", "Pipe");
            }
        }

        [RelayCommand]
        public async Task OpenPropertiesAsync()
        {
            if (_dialogService == null) return;
            var result = await _dialogService.ShowDashboardPropertiesAsync(CellWidth, ZoomScale);
            if (result != null)
            {
                if (Math.Abs(CellWidth - result.Value.CellSize) > 0.001 || Math.Abs(ZoomScale - result.Value.ZoomScale) > 0.001)
                {
                    UpdateGridAndScale(result.Value.CellSize, result.Value.ZoomScale);
                    RecordStep("Настройка параметров холста", "Settings");
                }
            }
        }

        [RelayCommand]
        public async Task AddWidgetAsync()
        {
            var selected = Widgets.FirstOrDefault(w => w.IsSelected);
            double col = selected != null ? selected.Col + 2 : 0;
            double row = selected != null ? selected.Row + 2 : 0;
            await AddWidgetAtAsync(col, row);
        }

        public async Task AddWidgetAtAsync(double col, double row)
        {
            if (_dialogService == null) return;

            var initialPos = new WidgetPosition { Col = col, Row = row, SizeX = 4, SizeY = 4 };
            var result = await _dialogService.ShowWidgetEditorAsync(null, _config.Connections, initialPos);
            if (result != null)
            {
                AddWidgetFromConfig(result);
            }
        }

        public void AddQuickWidgetAt(string type, double col, double row)
        {
            int valveSize = _config?.PipingPresets?.DefaultValveSize ?? 4;
            double pipeThickness = _config?.PipingPresets?.DefaultPipeThickness ?? 12.0;

            WidgetConfig config = type switch
            {
                "Valve" => new ValveConfig
                {
                    Title = "Клапан",
                    Position = new WidgetPosition { Col = col, Row = row, SizeX = valveSize, SizeY = valveSize },
                    ValveType = "CutOff",
                    ActiveColor = "#00FF00",
                    InactiveColor = "#FF0000"
                },
                "Pump" => new PumpConfig
                {
                    Title = "Насос",
                    Position = new WidgetPosition { Col = col, Row = row, SizeX = 6, SizeY = 6 },
                    ActiveColor = "#00FF00",
                    InactiveColor = "#FF0000"
                },
                "Pipe" => new PipeConfig
                {
                    Title = "Трубопровод",
                    Position = new WidgetPosition { Col = col, Row = row, SizeX = 10, SizeY = 2 },
                    PipePoints = "0,0;10,0",
                    Thickness = pipeThickness,
                    ShowFlanges = true,
                    ActiveColor = "#00FF00",
                    InactiveColor = "#AAAAAA"
                },
                "Tank" => new TankConfig
                {
                    Title = "Бак",
                    Position = new WidgetPosition { Col = col, Row = row, SizeX = 8, SizeY = 12 },
                    MinValue = 0,
                    MaxValue = 100
                },
                "HeatExchanger" => new HeatExchangerConfig
                {
                    Title = "Теплообменник",
                    Position = new WidgetPosition { Col = col, Row = row, SizeX = 8, SizeY = 8 },
                    ExchangerType = "ShellAndTube"
                },
                "Reactor" => new ReactorConfig
                {
                    Title = "Реактор",
                    Position = new WidgetPosition { Col = col, Row = row, SizeX = 10, SizeY = 14 },
                    HasJacket = true,
                    MinValue = 0,
                    MaxValue = 100
                },
                "LevelSensor" => new LevelSensorConfig
                {
                    Title = "LT",
                    TagNumber = "LT-01",
                    Position = new WidgetPosition { Col = col, Row = row, SizeX = 4, SizeY = 6 },
                    SensorType = "Radar",
                    MinValue = 0,
                    MaxValue = 100
                },
                "ContainerButton" => new ContainerButtonConfig
                {
                    Title = "Контейнер",
                    Position = new WidgetPosition { Col = col, Row = row, SizeX = 6, SizeY = 3 },
                    CellSize = (int)CellWidth,
                    ZoomScale = 1.0,
                    Children = new System.Collections.Generic.List<WidgetConfig>()
                },
                "ValueDisplay" => new ValueDisplayConfig
                {
                    Title = "Параметр",
                    Position = new WidgetPosition { Col = col, Row = row, SizeX = 6, SizeY = 3 },
                    Format = "{0:F1}"
                },
                "SetValue" => new SetValueConfig
                {
                    Title = "Уставка",
                    Position = new WidgetPosition { Col = col, Row = row, SizeX = 6, SizeY = 3 },
                    MinValue = 0,
                    MaxValue = 100,
                    Format = "{0:F1}"
                },
                "RealTimeTrend" => new RealTimeTrendConfig
                {
                    Title = "Тренд",
                    Position = new WidgetPosition { Col = col, Row = row, SizeX = 12, SizeY = 8 },
                    MinValue = 0,
                    MaxValue = 100
                },
                "CommandButton" => new CommandButtonConfig
                {
                    Title = "Команда",
                    Position = new WidgetPosition { Col = col, Row = row, SizeX = 6, SizeY = 3 },
                    ButtonMode = "Toggle"
                },
                "PilotLight" => new PilotLightConfig
                {
                    Title = "Индикатор",
                    Position = new WidgetPosition { Col = col, Row = row, SizeX = 4, SizeY = 4 }
                },
                "Slider" => new SliderConfig
                {
                    Title = "Ползунок",
                    Position = new WidgetPosition { Col = col, Row = row, SizeX = 8, SizeY = 3 },
                    MinValue = 0,
                    MaxValue = 100
                },
                _ => new ValueDisplayConfig
                {
                    Title = "Элемент",
                    Position = new WidgetPosition { Col = col, Row = row, SizeX = 4, SizeY = 4 }
                }
            };
            config.Type = type;
            AddWidgetFromConfig(config);
        }

        [RelayCommand]
        private void RemoveWidget(WidgetViewModelBase? widget)
        {
            widget ??= Widgets.FirstOrDefault(w => w.IsSelected);
            if (widget == null) return;

            string title = widget.Title;
            widget.IsSelected = false;

            // Remove from config model
            _dashboardConfig.Widgets.Remove(widget.OriginalConfig);
            
            // Remove VM and dispose
            widget.PropertyChanged -= OnWidgetPropertyChanged;
            widget.Dispose();
            Widgets.Remove(widget);
            UpdateSelectedWidgetsInfo();
            ResolvePipeConnections();
            RecordStep($"Удаление: {title}", "Delete");
        }

        [RelayCommand]
        public void RemoveSelectedWidgets()
        {
            var selectedList = Widgets.Where(w => w.IsSelected).ToList();
            if (selectedList.Count == 0) return;

            int count = selectedList.Count;
            foreach (var w in selectedList)
            {
                w.IsSelected = false;
                _dashboardConfig.Widgets.Remove(w.OriginalConfig);
                w.PropertyChanged -= OnWidgetPropertyChanged;
                w.Dispose();
                Widgets.Remove(w);
            }
            UpdateSelectedWidgetsInfo();
            ResolvePipeConnections();
            RecordStep($"Удаление элементов ({count})", "Delete");
        }

        [RelayCommand]
        public void ClearSelection()
        {
            foreach (var w in Widgets)
            {
                w.IsSelected = false;
            }
            UpdateSelectedWidgetsInfo();
        }

        [RelayCommand]
        public void ToggleMultiSelectMode()
        {
            IsMultiSelectMode = !IsMultiSelectMode;
        }

        [RelayCommand]
        public void CopyWidget(WidgetViewModelBase? widget)
        {
            widget ??= Widgets.FirstOrDefault(w => w.IsSelected);
            if (widget == null) return;
            WidgetClipboard.Copy(widget.OriginalConfig);
        }

        [RelayCommand]
        public void PasteWidget(object? parameter = null)
        {
            var clonedConfig = WidgetClipboard.PasteClone();
            if (clonedConfig == null) return;

            if (parameter is Avalonia.Point pt)
            {
                clonedConfig.Position.Col = Math.Max(0, Math.Floor(pt.X));
                clonedConfig.Position.Row = Math.Max(0, Math.Floor(pt.Y));
            }
            else
            {
                var selected = Widgets.FirstOrDefault(w => w.IsSelected);
                if (selected != null)
                {
                    clonedConfig.Position.Col = selected.Col + 2;
                    clonedConfig.Position.Row = selected.Row + 2;
                }
                else
                {
                    clonedConfig.Position.Col += 2;
                    clonedConfig.Position.Row += 2;
                }
            }

            AddWidgetFromConfig(clonedConfig);
        }

        public void PasteWidgetAt(double col, double row)
        {
            var clonedConfig = WidgetClipboard.PasteClone();
            if (clonedConfig == null) return;

            clonedConfig.Position.Col = col;
            clonedConfig.Position.Row = row;

            AddWidgetFromConfig(clonedConfig);
        }

        public void AddWidgetFromConfig(WidgetConfig config)
        {
            if (!_dashboardConfig.Widgets.Contains(config))
            {
                _dashboardConfig.Widgets.Add(config);
            }

            var vm = CreateWidgetViewModel(config);
            if (vm != null)
            {
                foreach (var w in Widgets) w.IsSelected = false;
                vm.IsSelected = true;
                vm.PropertyChanged += OnWidgetPropertyChanged;
                Widgets.Add(vm);
                UpdateSelectedWidgetsInfo();
                ResolvePipeConnections();
                RecordStep($"Добавление: {config.Title}", "Add");
            }
        }

        [RelayCommand]
        private void DuplicateWidget(WidgetViewModelBase? widget)
        {
            widget ??= Widgets.FirstOrDefault(w => w.IsSelected);
            if (widget == null) return;

            var sourceConfig = widget.OriginalConfig;
            var json = System.Text.Json.JsonSerializer.Serialize(sourceConfig);
            var clonedConfig = System.Text.Json.JsonSerializer.Deserialize<WidgetConfig>(json);

            if (clonedConfig != null)
            {
                WidgetClipboard.AssignFreshIds(clonedConfig);
                clonedConfig.Position.Row += 2;
                clonedConfig.Position.Col += 2;
                AddWidgetFromConfig(clonedConfig);
            }
        }

        [RelayCommand]
        private async Task EditWidgetAsync(WidgetViewModelBase? widget)
        {
            if (widget == null || _dialogService == null) return;

            var result = await _dialogService.ShowWidgetEditorAsync(widget.OriginalConfig, _config.Connections);
            if (result != null)
            {
                result.Id = widget.OriginalConfig.Id;
                result.GroupId = widget.OriginalConfig.GroupId;
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
                    newVm.IsSelected = widget.IsSelected;
                    newVm.PropertyChanged += OnWidgetPropertyChanged;
                    Widgets.RemoveAt(vmIndex);
                    Widgets.Insert(vmIndex, newVm);
                    ResolvePipeConnections();
                    RecordStep($"Настройка: {newVm.Title}", "Settings");
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
            if (_isReconciling) return;

            if (e.PropertyName == nameof(WidgetViewModelBase.Col) || 
                e.PropertyName == nameof(WidgetViewModelBase.Row) || 
                e.PropertyName == "PipePoints")
            {
                ResolvePipeConnections();
            }
            else if (e.PropertyName == nameof(WidgetViewModelBase.IsSelected))
            {
                UpdateSelectedWidgetsInfo();
            }
        }

        public void UpdateSelectedWidgetsInfo()
        {
            var selectedList = Widgets.Where(w => w.IsSelected).ToList();
            SelectedWidgetsCount = selectedList.Count;
            if (selectedList.Count == 0)
            {
                SelectedWidgetsSummary = string.Empty;
            }
            else
            {
                var groupSummary = selectedList
                    .GroupBy(w => string.IsNullOrEmpty(w.Type) ? "Widget" : w.Type)
                    .Select(g => $"{g.Key}: {g.Count()}");
                SelectedWidgetsSummary = string.Join(", ", groupSummary);
            }
            OnPropertyChanged(nameof(CanGroup));
            OnPropertyChanged(nameof(CanUngroup));
            CanAssembleCollinear = CollinearAssemblyService.CanAssemble(selectedList);
        }

        #region Alignment, Distribution & Group Commands

        [RelayCommand]
        public void AlignLeft()
        {
            var selected = Widgets.Where(w => w.IsSelected).ToList();
            if (selected.Count < 2) return;

            double minCol = selected.Min(w => w.Col);
            foreach (var w in selected)
            {
                w.Col = minCol;
                w.OriginalConfig.Position.Col = minCol;
            }
            ResolvePipeConnections();
            RecordStep("Выравнивание по левому краю", "Layout");
        }

        [RelayCommand]
        public void AlignRight()
        {
            var selected = Widgets.Where(w => w.IsSelected).ToList();
            if (selected.Count < 2) return;

            double maxRight = selected.Max(w => w.Col + w.SizeX);
            foreach (var w in selected)
            {
                double targetCol = Math.Max(0, maxRight - w.SizeX);
                w.Col = targetCol;
                w.OriginalConfig.Position.Col = targetCol;
            }
            ResolvePipeConnections();
            RecordStep("Выравнивание по правому краю", "Layout");
        }

        [RelayCommand]
        public void AlignCenterHorizontal()
        {
            var selected = Widgets.Where(w => w.IsSelected).ToList();
            if (selected.Count < 2) return;

            double minCol = selected.Min(w => w.Col);
            double maxRight = selected.Max(w => w.Col + w.SizeX);
            double groupCenter = (minCol + maxRight) / 2.0;

            foreach (var w in selected)
            {
                double targetCol = Math.Max(0, Math.Round(groupCenter - w.SizeX / 2.0));
                w.Col = targetCol;
                w.OriginalConfig.Position.Col = targetCol;
            }
            ResolvePipeConnections();
            RecordStep("Выравнивание по центру (гор.)", "Layout");
        }

        [RelayCommand]
        public void AlignTop()
        {
            var selected = Widgets.Where(w => w.IsSelected).ToList();
            if (selected.Count < 2) return;

            double minRow = selected.Min(w => w.Row);
            foreach (var w in selected)
            {
                w.Row = minRow;
                w.OriginalConfig.Position.Row = minRow;
            }
            ResolvePipeConnections();
            RecordStep("Выравнивание по верхнему краю", "Layout");
        }

        [RelayCommand]
        public void AlignBottom()
        {
            var selected = Widgets.Where(w => w.IsSelected).ToList();
            if (selected.Count < 2) return;

            double maxBottom = selected.Max(w => w.Row + w.SizeY);
            foreach (var w in selected)
            {
                double targetRow = Math.Max(0, maxBottom - w.SizeY);
                w.Row = targetRow;
                w.OriginalConfig.Position.Row = targetRow;
            }
            ResolvePipeConnections();
            RecordStep("Выравнивание по нижнему краю", "Layout");
        }

        [RelayCommand]
        public void AlignCenterVertical()
        {
            var selected = Widgets.Where(w => w.IsSelected).ToList();
            if (selected.Count < 2) return;

            double minRow = selected.Min(w => w.Row);
            double maxBottom = selected.Max(w => w.Row + w.SizeY);
            double groupCenter = (minRow + maxBottom) / 2.0;

            foreach (var w in selected)
            {
                double targetRow = Math.Max(0, Math.Round(groupCenter - w.SizeY / 2.0));
                w.Row = targetRow;
                w.OriginalConfig.Position.Row = targetRow;
            }
            ResolvePipeConnections();
            RecordStep("Выравнивание по центру (верт.)", "Layout");
        }

        [RelayCommand]
        public void DistributeHorizontally()
        {
            var selected = Widgets.Where(w => w.IsSelected).OrderBy(w => w.Col).ToList();
            if (selected.Count < 3) return;

            double firstLeft = selected.First().Col;
            double lastLeft = selected.Last().Col;
            double totalSpan = lastLeft - firstLeft;
            double step = totalSpan / (selected.Count - 1);

            for (int i = 0; i < selected.Count; i++)
            {
                double targetCol = Math.Max(0, Math.Round(firstLeft + i * step));
                selected[i].Col = targetCol;
                selected[i].OriginalConfig.Position.Col = targetCol;
            }
            ResolvePipeConnections();
            RecordStep("Распределение по горизонтали", "Layout");
        }

        [RelayCommand]
        public void DistributeVertically()
        {
            var selected = Widgets.Where(w => w.IsSelected).OrderBy(w => w.Row).ToList();
            if (selected.Count < 3) return;

            double firstTop = selected.First().Row;
            double lastTop = selected.Last().Row;
            double totalSpan = lastTop - firstTop;
            double step = totalSpan / (selected.Count - 1);

            for (int i = 0; i < selected.Count; i++)
            {
                double targetRow = Math.Max(0, Math.Round(firstTop + i * step));
                selected[i].Row = targetRow;
                selected[i].OriginalConfig.Position.Row = targetRow;
            }
            ResolvePipeConnections();
            RecordStep("Распределение по вертикали", "Layout");
        }

        [RelayCommand]
        public void GroupSelectedWidgets()
        {
            var selected = Widgets.Where(w => w.IsSelected).ToList();
            if (selected.Count < 2) return;

            string newGroupId = Guid.NewGuid().ToString("N")[..8];
            foreach (var w in selected)
            {
                w.GroupId = newGroupId;
            }
            UpdateSelectedWidgetsInfo();
            RecordStep("Группировка элементов", "Layout");
        }

        [RelayCommand]
        public void UngroupSelectedWidgets()
        {
            var selected = Widgets.Where(w => w.IsSelected).ToList();
            foreach (var w in selected)
            {
                w.GroupId = null;
            }
            UpdateSelectedWidgetsInfo();
            RecordStep("Разгруппировка элементов", "Layout");
        }

        [RelayCommand]
        public void DuplicateSelectedWidgets()
        {
            var selected = Widgets.Where(w => w.IsSelected).ToList();
            if (selected.Count == 0) return;

            var newConfigs = new List<WidgetConfig>();
            foreach (var w in selected)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(w.OriginalConfig);
                var clone = System.Text.Json.JsonSerializer.Deserialize<WidgetConfig>(json);
                if (clone != null)
                {
                    WidgetClipboard.AssignFreshIds(clone);
                    clone.Position.Col += 2;
                    clone.Position.Row += 2;
                    newConfigs.Add(clone);
                }
            }

            foreach (var w in Widgets) w.IsSelected = false;

            foreach (var cfg in newConfigs)
            {
                _dashboardConfig.Widgets.Add(cfg);
                var vm = CreateWidgetViewModel(cfg);
                if (vm != null)
                {
                    vm.IsSelected = true;
                    vm.PropertyChanged += OnWidgetPropertyChanged;
                    Widgets.Add(vm);
                }
            }

            UpdateSelectedWidgetsInfo();
            ResolvePipeConnections();
            RecordStep($"Дублирование элементов ({newConfigs.Count})", "Add");
        }

        #endregion

        public void ResolvePipeConnections()
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

        [RelayCommand]
        public void ToggleRubberBanding()
        {
            IsRubberBandingEnabled = !IsRubberBandingEnabled;
        }

        [RelayCommand]
        public void AssembleCollinearNode()
        {
            var selected = Widgets.Where(w => w.IsSelected).ToList();
            if (CollinearAssemblyService.AssembleCollinear(selected))
            {
                ResolvePipeConnections();
                UpdateSelectedWidgetsInfo();
                RecordStep("Соосная сборка узла", "Pipe");
            }
        }

        [RelayCommand]
        public void AttachPipesToValve(ValveWidgetViewModel? valve)
        {
            if (valve == null) return;
            double thickness = _config?.PipingPresets?.DefaultPipeThickness ?? 12.0;
            var (p1Cfg, p2Cfg) = CollinearAssemblyService.CreatePipesForValveFlanges(valve, pipeThickness: thickness);
            _dashboardConfig.Widgets.Add(p1Cfg);
            _dashboardConfig.Widgets.Add(p2Cfg);

            var vm1 = CreateWidgetViewModel(p1Cfg);
            var vm2 = CreateWidgetViewModel(p2Cfg);

            if (vm1 != null)
            {
                vm1.PropertyChanged += OnWidgetPropertyChanged;
                Widgets.Add(vm1);
            }
            if (vm2 != null)
            {
                vm2.PropertyChanged += OnWidgetPropertyChanged;
                Widgets.Add(vm2);
            }
            ResolvePipeConnections();
            RecordStep("Подключение труб к фланцам", "Pipe");
        }

        public (PipeWidgetViewModel? PipeA, PipeWidgetViewModel? PipeB) ReplacePipeWithSplit(
            PipeWidgetViewModel originalPipe, 
            PipeConfig pipeACfg, 
            PipeConfig pipeBCfg,
            bool recordHistoryStep = true)
        {
            int origIndex = Widgets.IndexOf(originalPipe);
            originalPipe.PropertyChanged -= OnWidgetPropertyChanged;
            originalPipe.Dispose();
            Widgets.Remove(originalPipe);
            _dashboardConfig.Widgets.Remove(originalPipe.OriginalConfig);

            _dashboardConfig.Widgets.Add(pipeACfg);
            _dashboardConfig.Widgets.Add(pipeBCfg);

            var vmA = CreateWidgetViewModel(pipeACfg) as PipeWidgetViewModel;
            var vmB = CreateWidgetViewModel(pipeBCfg) as PipeWidgetViewModel;

            if (vmA != null)
            {
                vmA.PropertyChanged += OnWidgetPropertyChanged;
                if (origIndex >= 0 && origIndex <= Widgets.Count)
                    Widgets.Insert(origIndex, vmA);
                else
                    Widgets.Add(vmA);
            }
            if (vmB != null)
            {
                vmB.PropertyChanged += OnWidgetPropertyChanged;
                if (origIndex >= 0 && origIndex + 1 <= Widgets.Count)
                    Widgets.Insert(origIndex + 1, vmB);
                else
                    Widgets.Add(vmB);
            }

            ResolvePipeConnections();
            if (recordHistoryStep)
            {
                RecordStep("Врезка в трубопровод", "Pipe");
            }
            return (vmA, vmB);
        }

        public void Dispose()
        {
            _historyService.Dispose();
            foreach (var widget in Widgets)
            {
                widget.PropertyChanged -= OnWidgetPropertyChanged;
                widget.Dispose();
            }
            Widgets.Clear();
        }
    }
}
