using System;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication1.Services
{
    public class ThemeService : ObservableObject, IThemeService
    {
        private string _currentTheme = "Dark";

        public string CurrentTheme
        {
            get => _currentTheme;
            private set
            {
                if (SetProperty(ref _currentTheme, value))
                {
                    OnPropertyChanged(nameof(IsDarkTheme));
                    OnPropertyChanged(nameof(ThemeToggleGlyph));
                }
            }
        }

        public bool IsDarkTheme => !string.Equals(CurrentTheme, "Light", StringComparison.OrdinalIgnoreCase);

        public string ThemeToggleGlyph => IsDarkTheme ? "🌙" : "☀️";

        public void SetTheme(string theme)
        {
            var normalized = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
            CurrentTheme = normalized;

            void ApplyTheme()
            {
                if (Application.Current != null)
                {
                    Application.Current.RequestedThemeVariant = normalized == "Light"
                        ? ThemeVariant.Light
                        : ThemeVariant.Dark;
                }
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                ApplyTheme();
            }
            else
            {
                Dispatcher.UIThread.Post(ApplyTheme);
            }
        }

        public void ToggleTheme()
        {
            SetTheme(IsDarkTheme ? "Light" : "Dark");
        }
    }
}
