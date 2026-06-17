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

        private readonly List<WidgetConfig> _childrenConfig;
        private readonly IDialogService? _dialogService;

        public ContainerButtonViewModel(ContainerButtonConfig config, IDataCoreService dataService, IProjectContextService projectContext) 
            : base(config, dataService, projectContext)
        {
            _childrenConfig = config.Children ?? new List<WidgetConfig>();
            if (App.Services != null)
            {
                _dialogService = App.Services.GetService<IDialogService>();
            }
        }

        [RelayCommand]
        private async Task OpenContainerAsync()
        {
            if (ProjectContext.IsDesignMode) return;

            var mainVm = App.Services?.GetService<MainViewModel>();
            if (mainVm != null)
            {
                var existing = mainVm.ActiveChildWindows.FirstOrDefault(w => w.Title == Title);
                if (existing != null)
                {
                    existing.CloseAction?.Invoke();
                    return;
                }

                var dashboardConfig = new DashboardConfig
                {
                    Widgets = _childrenConfig
                };
                mainVm.OpenChildWindow(Title, dashboardConfig);
            }
            else if (_dialogService != null)
            {
                var dashboardConfig = new DashboardConfig
                {
                    Widgets = _childrenConfig
                };
                await _dialogService.ShowContainerDashboardAsync(Title, dashboardConfig);
            }
        }
    }
}
