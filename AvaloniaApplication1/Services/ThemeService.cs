using System;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication1.Services
{
    public class ThemeService : ObservableObject, IThemeService
    {
        public static readonly ThemeVariant IndustrialGrayVariant = new("IndustrialGray", ThemeVariant.Light);

        private string _currentTheme = "Dark";

        public string CurrentTheme
        {
            get => _currentTheme;
            private set
            {
                if (SetProperty(ref _currentTheme, value))
                {
                    OnPropertyChanged(nameof(IsDarkTheme));
                    OnPropertyChanged(nameof(IsIndustrialGrayTheme));
                    OnPropertyChanged(nameof(ThemeToggleGlyph));
                }
            }
        }

        public bool IsDarkTheme => string.Equals(CurrentTheme, "Dark", StringComparison.OrdinalIgnoreCase);

        public bool IsIndustrialGrayTheme => string.Equals(CurrentTheme, "IndustrialGray", StringComparison.OrdinalIgnoreCase);

        public string ThemeToggleGlyph => CurrentTheme switch
        {
            "Light" => "☀️",
            "IndustrialGray" => "🏭",
            _ => "🌙"
        };

        public void SetTheme(string theme)
        {
            var normalized = theme?.Trim() switch
            {
                var t when string.Equals(t, "Light", StringComparison.OrdinalIgnoreCase) => "Light",
                var t when string.Equals(t, "IndustrialGray", StringComparison.OrdinalIgnoreCase) => "IndustrialGray",
                _ => "Dark"
            };
            CurrentTheme = normalized;

            void ApplyTheme()
            {
                if (Application.Current != null)
                {
                    Application.Current.RequestedThemeVariant = normalized switch
                    {
                        "Light" => ThemeVariant.Light,
                        "IndustrialGray" => IndustrialGrayVariant,
                        _ => ThemeVariant.Dark
                    };
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
            var next = CurrentTheme switch
            {
                "Dark" => "Light",
                "Light" => "IndustrialGray",
                _ => "Dark"
            };
            SetTheme(next);
        }
    }
}
