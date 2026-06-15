using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvaloniaApplication1.Views
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
