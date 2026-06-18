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
        private readonly List<WidgetConfig> _childrenConfig;
        public ContainerButtonViewModel(ContainerButtonConfig config, IDataCoreService dataService, IProjectContextService projectContext) 
            : base(config, dataService, projectContext)
        {
            _childrenConfig = config.Children ?? new List<WidgetConfig>();
        }

        [RelayCommand]
        private async Task OpenContainerAsync()
        {
            if (ProjectContext.IsDesignMode) return;

            if (_controlWindow != null)
            {
                _controlWindow.CloseAction?.Invoke();
                _controlWindow = null;
                return;
            }

            var dashboardConfig = new DashboardConfig { Widgets = _childrenConfig };

            if (ChildWindowService != null)
            {
                _controlWindow = ChildWindowService.OpenChildWindow(Title, (object)dashboardConfig);
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
            else if (DialogService != null)
            {
                await DialogService.ShowContainerDashboardAsync(Title, dashboardConfig);
            }
        }
    }
}
