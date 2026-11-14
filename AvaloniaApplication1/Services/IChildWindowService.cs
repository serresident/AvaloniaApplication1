using AvaloniaApplication1.ViewModels.Core;

namespace AvaloniaApplication1.Services
{
    public interface IChildWindowService
    {
        ChildWindowViewModel? OpenChildWindow(string title, object content);
    }
}
