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
            if (string.IsNullOrEmpty(config.Type))
            {
                config.Type = config.GetType().Name.Replace("Config", "");
            }
            CopiedWidgetJson = JsonSerializer.Serialize(config, config.GetType());
        }

        public static WidgetConfig? PasteClone()
        {
            if (string.IsNullOrWhiteSpace(CopiedWidgetJson)) return null;

            try
            {
                var clone = JsonSerializer.Deserialize<WidgetConfig>(CopiedWidgetJson);
                if (clone != null)
                {
                    AssignFreshIds(clone);
                }
                return clone;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WidgetClipboard] Error pasting widget: {ex.Message}");
                return null;
            }
        }

        public static void AssignFreshIds(WidgetConfig? config)
        {
            if (config == null) return;
            config.Id = Guid.NewGuid();
            if (config is ContainerButtonConfig container && container.Children != null)
            {
                foreach (var child in container.Children)
                {
                    AssignFreshIds(child);
                }
            }
        }

        public static void Clear()
        {
            CopiedWidgetJson = null;
        }
    }
}
