using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels
{
    public partial class ConnectionManagerViewModel : ViewModelBase
    {
        private readonly IDialogService _dialogService;
        private readonly HmiConfiguration _config;

        public ObservableCollection<ConnectionConfig> Connections { get; }

        [ObservableProperty]
        private ConnectionConfig? _selectedConnection;

        public ConnectionManagerViewModel(HmiConfiguration config, IDialogService dialogService)
        {
            _config = config;
            _dialogService = dialogService;
            Connections = new ObservableCollection<ConnectionConfig>(config.Connections);
        }

        [RelayCommand]
        private async Task AddConnectionAsync()
        {
            var result = await _dialogService.ShowConnectionEditorAsync(null);
            if (result != null)
            {
                Connections.Add(result);
                _config.Connections.Add(result);
            }
        }

        [RelayCommand]
        private async Task EditConnectionAsync()
        {
            if (SelectedConnection == null) return;

            var result = await _dialogService.ShowConnectionEditorAsync(SelectedConnection);
            if (result != null)
            {
                var index = Connections.IndexOf(SelectedConnection);
                if (index >= 0)
                {
                    // Update in both collections
                    var configIndex = _config.Connections.IndexOf(SelectedConnection);
                    
                    Connections[index] = result;
                    if (configIndex >= 0)
                        _config.Connections[configIndex] = result;
                    
                    SelectedConnection = result;
                }
            }
        }

        [RelayCommand]
        private void DeleteConnection()
        {
            if (SelectedConnection == null) return;

            _config.Connections.Remove(SelectedConnection);
            Connections.Remove(SelectedConnection);
            SelectedConnection = null;
        }
    }
}
