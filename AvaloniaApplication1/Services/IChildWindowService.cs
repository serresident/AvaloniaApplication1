using AvaloniaApplication1.ViewModels;

namespace AvaloniaApplication1.Services
{
    public interface IChildWindowService
    {
        ChildWindowViewModel? OpenChildWindow(string title, object content);
    }
}
