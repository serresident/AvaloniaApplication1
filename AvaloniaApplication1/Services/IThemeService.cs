using System.ComponentModel;

namespace AvaloniaApplication1.Services
{
    /// <summary>
    /// Service for managing runtime application themes (Dark, Light, IndustrialGray) with seamless switching and persistence.
    /// </summary>
    public interface IThemeService : INotifyPropertyChanged
    {
        /// <summary>
        /// Current active theme name ("Dark", "Light", or "IndustrialGray").
        /// </summary>
        string CurrentTheme { get; }

        /// <summary>
        /// Returns true if currently in Dark theme.
        /// </summary>
        bool IsDarkTheme { get; }

        /// <summary>
        /// Returns true if currently in ISA-101 IndustrialGray theme.
        /// </summary>
        bool IsIndustrialGrayTheme { get; }

        /// <summary>
        /// Visual glyph for quick toggling (e.g. 🌙 in Dark, ☀️ in Light, 🏭 in IndustrialGray).
        /// </summary>
        string ThemeToggleGlyph { get; }

        /// <summary>
        /// Sets the active theme by name ("Dark", "Light", or "IndustrialGray").
        /// </summary>
        void SetTheme(string theme);

        /// <summary>
        /// Cycles through Dark -> Light -> IndustrialGray -> Dark themes.
        /// </summary>
        void ToggleTheme();
    }
}
