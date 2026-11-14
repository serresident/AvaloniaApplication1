using AvaloniaApplication1.Services;
using AvaloniaApplication1.Models.Config;

namespace AvaloniaApplication1.ViewModels.Core
{
    public interface IWidgetFactory
    {
        WidgetViewModelBase CreateWidgetViewModel(WidgetConfig config);
    }
}