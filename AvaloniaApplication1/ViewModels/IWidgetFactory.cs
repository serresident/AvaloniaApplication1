using AvaloniaApplication1.Models.Config;

namespace AvaloniaApplication1.ViewModels
{
    public interface IWidgetFactory
    {
        WidgetViewModelBase CreateWidgetViewModel(WidgetConfig config);
    }
}