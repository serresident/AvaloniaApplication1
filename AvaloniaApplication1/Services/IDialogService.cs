using System.Collections.Generic;
using System.Threading.Tasks;
using AvaloniaApplication1.Models.Config;

namespace AvaloniaApplication1.Services
{
    public interface IDialogService
    {
        Task<string?> ShowNumpadAsync(string title, string initialValue, double? x = null, double? y = null);
        Task ShowContainerDashboardAsync(string title, DashboardConfig config);

        // Design Mode dialogs
        Task<WidgetConfig?> ShowWidgetEditorAsync(WidgetConfig? existingConfig, List<ConnectionConfig> connections, WidgetPosition? initialPosition = null);
        Task<(double CellSize, double ZoomScale)?> ShowDashboardPropertiesAsync(double currentCellSize, double currentZoomScale);
        Task<ConnectionConfig?> ShowConnectionEditorAsync(ConnectionConfig? existingConfig);
        Task ShowConnectionManagerAsync(HmiConfiguration config);

        // Project File dialogs
        Task<string?> ShowOpenProjectDialogAsync();
        Task<string?> ShowSaveProjectAsDialogAsync(string? defaultFileName = null);
    }
}