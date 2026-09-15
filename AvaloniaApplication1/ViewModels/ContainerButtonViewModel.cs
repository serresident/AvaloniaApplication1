using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace AvaloniaApplication1.ViewModels
{
    public partial class ContainerButtonViewModel : WidgetViewModelBase
    {
        public ContainerButtonConfig TypedConfig => (ContainerButtonConfig)OriginalConfig;

        private ChildWindowViewModel? _controlWindow;
        private double? _lastPopupX;
        private double? _lastPopupY;
        private readonly List<WidgetConfig> _childrenConfig;
        public ContainerButtonViewModel(ContainerButtonConfig config, IDataCoreService dataService, IProjectContextService projectContext) 
            : base(config, dataService, projectContext)
        {
            _childrenConfig = config.Children ?? new List<WidgetConfig>();
        }

        [RelayCommand]
        private async Task OpenContainerAsync()
        {
            if (_controlWindow != null)
            {
                _controlWindow.CloseAction?.Invoke();
                _controlWindow = null;
                return;
            }

            var dashboardConfig = new DashboardConfig 
            { 
                CellSize = TypedConfig.CellSize > 0 ? TypedConfig.CellSize : 40,
                ZoomScale = TypedConfig.ZoomScale > 0 ? TypedConfig.ZoomScale : 1.0,
                Widgets = _childrenConfig 
            };

            if (ChildWindowService != null)
            {
                _controlWindow = ChildWindowService.OpenChildWindow(Title, (object)dashboardConfig);
                if (_controlWindow != null)
                {
                    if (_controlWindow.Content is DashboardViewModel innerDashboardVm)
                    {
                        innerDashboardVm.OnGridOrScaleChanged += (cellSize, zoomScale) =>
                        {
                            TypedConfig.CellSize = (int)System.Math.Round(cellSize);
                            TypedConfig.ZoomScale = zoomScale;
                        };
                    }

                    if (_lastPopupX.HasValue && _lastPopupY.HasValue)
                    {
                        _controlWindow.X = _lastPopupX.Value;
                        _controlWindow.Y = _lastPopupY.Value;
                    }

                    var originalClose = _controlWindow.CloseAction;
                    _controlWindow.CloseAction = () =>
                    {
                        if (_controlWindow != null)
                        {
                            _lastPopupX = _controlWindow.X;
                            _lastPopupY = _controlWindow.Y;
                        }
                        originalClose?.Invoke();
                        _controlWindow = null;
                    };
                }
            }
            else if (DialogService != null)
            {
                await DialogService.ShowContainerDashboardAsync(Title, dashboardConfig);
                TypedConfig.CellSize = dashboardConfig.CellSize;
                TypedConfig.ZoomScale = dashboardConfig.ZoomScale;
            }
        }

        [RelayCommand]
        public void PasteWidgetIntoContainer()
        {
            var clonedConfig = WidgetClipboard.PasteClone();
            if (clonedConfig == null) return;

            if (TypedConfig.Children == null)
            {
                TypedConfig.Children = new List<WidgetConfig>();
            }

            // Position at beginning or below existing widgets
            double maxRow = TypedConfig.Children.Count > 0
                ? TypedConfig.Children.Max(c => c.Position.Row + c.Position.SizeY)
                : 0;
            clonedConfig.Position.Col = 0;
            clonedConfig.Position.Row = maxRow > 0 ? maxRow + 1 : 0;

            if (_controlWindow?.Content is DashboardViewModel innerDashboardVm)
            {
                innerDashboardVm.AddWidgetFromConfig(clonedConfig);
            }
            else
            {
                TypedConfig.Children.Add(clonedConfig);
                if (!_childrenConfig.Contains(clonedConfig))
                {
                    _childrenConfig.Add(clonedConfig);
                }
            }
        }
    }
}
