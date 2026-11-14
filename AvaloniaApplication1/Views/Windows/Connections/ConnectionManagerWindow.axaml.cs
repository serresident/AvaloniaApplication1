using AvaloniaApplication1.ViewModels.Windows.Connections;
using AvaloniaApplication1.ViewModels.Core;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvaloniaApplication1.Views.Windows.Connections
{
    public partial class ConnectionManagerWindow : Window
    {
        public ConnectionManagerWindow()
        {
            InitializeComponent();
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
