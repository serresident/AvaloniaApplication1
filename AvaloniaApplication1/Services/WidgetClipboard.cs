using System;
using System.Text.Json;
using AvaloniaApplication1.Models.Config;

namespace AvaloniaApplication1.Services
{
    public static class WidgetClipboard
    {
        public static string? CopiedWidgetJson { get; private set; }

        public static bool HasWidget => !string.IsNullOrWhiteSpace(CopiedWidgetJson);

        public static void Copy(WidgetConfig config)
        {
            if (config == null) return;
            CopiedWidgetJson = JsonSerializer.Serialize(config);
        }

        public static WidgetConfig? PasteClone()
        {
            if (string.IsNullOrWhiteSpace(CopiedWidgetJson)) return null;

            try
            {
                return JsonSerializer.Deserialize<WidgetConfig>(CopiedWidgetJson);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WidgetClipboard] Error pasting widget: {ex.Message}");
                return null;
            }
        }

        public static void Clear()
        {
            CopiedWidgetJson = null;
        }
    }
}
