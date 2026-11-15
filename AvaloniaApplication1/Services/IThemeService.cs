using System.ComponentModel;

namespace AvaloniaApplication1.Services
{
    /// <summary>
    /// Service for managing runtime application themes (Dark and Light) with seamless switching and persistence.
    /// </summary>
    public interface IThemeService : INotifyPropertyChanged
    {
        /// <summary>
        /// Current active theme name ("Dark" or "Light").
        /// </summary>
        string CurrentTheme { get; }

        /// <summary>
        /// Returns true if currently in Dark theme.
        /// </summary>
        bool IsDarkTheme { get; }

        /// <summary>
        /// Visual glyph for quick toggling (e.g. 🌙 in Dark, ☀️ in Light).
        /// </summary>
        string ThemeToggleGlyph { get; }

        /// <summary>
        /// Sets the active theme by name ("Dark" or "Light").
        /// </summary>
        void SetTheme(string theme);

        /// <summary>
        /// Toggles between Dark and Light themes.
        /// </summary>
        void ToggleTheme();
    }
}
