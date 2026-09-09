using System;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;

namespace AvaloniaApplication1.ViewModels
{
    public class WidgetFactory : IWidgetFactory
    {
        private readonly IDataCoreService _dataService;
        private readonly IProjectContextService _projectContext;
        private readonly IAlarmNotificationService _alarmService;
        public IChildWindowService? ChildWindowService { get; set; }
        public IDialogService? DialogService { get; set; }

        public WidgetFactory(IDataCoreService dataService, IProjectContextService projectContext,
            IAlarmNotificationService alarmService)
        {
            _dataService = dataService;
            _projectContext = projectContext;
            _alarmService = alarmService;
        }

        public WidgetViewModelBase CreateWidgetViewModel(WidgetConfig config)
        {
            WidgetViewModelBase vm = config switch
            {
                ValueDisplayConfig c => new ValueDisplayViewModel(c, _dataService, _projectContext),
                PilotLightConfig c => new PilotLightViewModel(c, _dataService, _projectContext),
                ContainerButtonConfig c => new ContainerButtonViewModel(c, _dataService, _projectContext),
                CommandButtonConfig c => new CommandButtonViewModel(c, _dataService, _projectContext),
                SliderConfig c => new SliderViewModel(c, _dataService, _projectContext),
                SetValueConfig c => new SetValueViewModel(c, _dataService, _projectContext),
                RealTimeTrendConfig c => new RealTimeTrendViewModel(c, _dataService, _projectContext),
                PipeConfig c => new PipeWidgetViewModel(c, _dataService, _projectContext),
                ValveConfig c => new ValveWidgetViewModel(c, _dataService, _projectContext),
                TankConfig c => new TankWidgetViewModel(c, _dataService, _projectContext),
                PumpConfig c => new PumpWidgetViewModel(c, _dataService, _projectContext),
                HeatExchangerConfig c => new HeatExchangerWidgetViewModel(c, _dataService, _projectContext),
                ReactorConfig c => new ReactorWidgetViewModel(c, _dataService, _projectContext),
                LevelSensorConfig c => new LevelSensorWidgetViewModel(c, _dataService, _projectContext),
                _ => throw new ArgumentException($"Unknown widget config type: {config.GetType().Name}")
            };

            vm.AlarmService = _alarmService;
            vm.ChildWindowService = ChildWindowService;
            vm.DialogService = DialogService;

            return vm;
        }
    }
}
