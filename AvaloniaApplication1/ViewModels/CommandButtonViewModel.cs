using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels
{
    public partial class CommandButtonViewModel : WidgetViewModelBase
    {
        public CommandButtonConfig TypedConfig => (CommandButtonConfig)OriginalConfig;

        public CommandButtonViewModel(CommandButtonConfig config, IDataCoreService dataService, IProjectContextService projectContext) 
            : base(config, dataService, projectContext)
        {
        }

        [RelayCommand]
        private void ExecuteCommand()
        {
            if (ProjectContext.IsDesignMode || Source == null) return;

            // Simple toggle simulation
            var current = DataService.GetCurrentValue(Source.ConnId, Source.Address);
            bool newState = true;
            
            if (current is bool b)
                newState = !b;
            else if (current is int i)
                newState = i == 0;

            DataService.WriteCommand(Source.ConnId, Source.Address, newState);
        }
    }
}
