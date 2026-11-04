using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Models.Config;

namespace AvaloniaApplication1.Services
{
    /// <summary>
    /// Automates one-time JSON configuration migration from Schema v1 (direct register binding)
    /// to Schema v2 (centralized TagManager architecture inspired by Rapid SCADA v6).
    /// </summary>
    public static class ConfigMigrationService
    {
        public const int CurrentSchemaVersion = 2;

        public static bool NeedsMigration(HmiConfiguration config)
        {
            if (config == null) return false;
            return config.SchemaVersion < CurrentSchemaVersion || config.Tags == null || (config.Tags.Count == 0 && HasAnyDataSources(config));
        }

        public static HmiConfiguration Migrate(HmiConfiguration config, string? sourceFilePath = null)
        {
            if (config == null) return new HmiConfiguration { SchemaVersion = CurrentSchemaVersion };

            // 1. Create backup if source file exists
            if (!string.IsNullOrWhiteSpace(sourceFilePath) && File.Exists(sourceFilePath))
            {
                try
                {
                    var dir = Path.GetDirectoryName(sourceFilePath) ?? string.Empty;
                    var backupPath = Path.Combine(dir, "config.v1.backup.json");
                    File.Copy(sourceFilePath, backupPath, overwrite: true);
                    Console.WriteLine($"[ConfigMigration] Schema v1 backup successfully created at: {backupPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ConfigMigration] Warning: Could not create config backup: {ex.Message}");
                }
            }

            config.Tags ??= new List<TagDefinition>();
            var tagMap = config.Tags.ToDictionary(t => t.TagId, StringComparer.OrdinalIgnoreCase);
            var addressMap = config.Tags
                .Where(t => !string.IsNullOrEmpty(t.ConnId) && !string.IsNullOrEmpty(t.Address))
                .ToDictionary(t => BuildAddressKey(t.ConnId, t.Address), t => t, StringComparer.OrdinalIgnoreCase);

            TagDefinition EnsureTag(DataSourceConfig? src, string? defaultTitle = null)
            {
                if (src == null || string.IsNullOrWhiteSpace(src.Address)) return null!;

                var key = BuildAddressKey(src.ConnId, src.Address);
                if (addressMap.TryGetValue(key, out var existingByAddress))
                {
                    src.TagId = existingByAddress.TagId;
                    return existingByAddress;
                }

                if (!string.IsNullOrWhiteSpace(src.TagId) && tagMap.TryGetValue(src.TagId, out var existingById))
                {
                    return existingById;
                }

                var tagId = GenerateTagId(src.ConnId, src.Address);
                int suffix = 1;
                var uniqueTagId = tagId;
                while (tagMap.ContainsKey(uniqueTagId))
                {
                    uniqueTagId = $"{tagId}_{suffix++}";
                }

                var newTag = new TagDefinition
                {
                    TagId = uniqueTagId,
                    Name = !string.IsNullOrWhiteSpace(defaultTitle) ? defaultTitle : uniqueTagId,
                    ConnId = src.ConnId ?? string.Empty,
                    Address = src.Address,
                    DataType = !string.IsNullOrWhiteSpace(src.DataType) ? src.DataType : "Float32",
                    StaleTimeout = TimeSpan.FromSeconds(5)
                };

                config.Tags.Add(newTag);
                tagMap[uniqueTagId] = newTag;
                addressMap[key] = newTag;
                src.TagId = uniqueTagId;
                return newTag;
            }

            void ScanWidgets(IEnumerable<WidgetConfig>? widgets)
            {
                if (widgets == null) return;
                foreach (var w in widgets)
                {
                    if (w.Source != null && !string.IsNullOrWhiteSpace(w.Source.Address))
                    {
                        EnsureTag(w.Source, w.Title);
                    }

                    if (w is ValveConfig vc)
                    {
                        if (vc.FeedbackSource != null) EnsureTag(vc.FeedbackSource, $"{w.Title}_Feedback");
                        if (vc.ClosedFeedbackSource != null) EnsureTag(vc.ClosedFeedbackSource, $"{w.Title}_ClosedFeedback");
                        if (vc.ModeSource != null) EnsureTag(vc.ModeSource, $"{w.Title}_Mode");
                    }
                    else if (w is HeatExchangerConfig he)
                    {
                        if (he.SecondarySource != null) EnsureTag(he.SecondarySource, $"{w.Title}_Secondary");
                    }
                    else if (w is ReactorConfig rc)
                    {
                        if (rc.AgitatorSource != null) EnsureTag(rc.AgitatorSource, $"{w.Title}_Agitator");
                        if (rc.TempSource != null) EnsureTag(rc.TempSource, $"{w.Title}_Temp");
                    }
                    else if (w is ContainerButtonConfig cb && cb.Children != null)
                    {
                        ScanWidgets(cb.Children);
                    }
                }
            }

            ScanWidgets(config.Dashboard?.Widgets);
            ScanWidgets(config.Mimic?.Widgets);

            if (config.BridgeRules != null)
            {
                foreach (var br in config.BridgeRules)
                {
                    if (br.Source != null && !string.IsNullOrWhiteSpace(br.Source.Address))
                    {
                        var ds = new DataSourceConfig { ConnId = br.Source.ConnId, Address = br.Source.Address };
                        var t = EnsureTag(ds, $"{br.Name}_Src");
                        br.Source.TagId = t.TagId;
                    }
                    if (br.Destination != null && !string.IsNullOrWhiteSpace(br.Destination.Address))
                    {
                        var ds = new DataSourceConfig { ConnId = br.Destination.ConnId, Address = br.Destination.Address };
                        var t = EnsureTag(ds, $"{br.Name}_Dst");
                        br.Destination.TagId = t.TagId;
                    }
                }
            }

            config.SchemaVersion = CurrentSchemaVersion;
            Console.WriteLine($"[ConfigMigration] Migration to Schema v2 completed: {config.Tags.Count} tags registered in centralized dictionary.");
            return config;
        }

        public static string GenerateTagId(string? connId, string? address)
        {
            var c = (connId ?? "conn").Trim().Replace('/', '_').Replace('.', '_').Replace(':', '_').Replace('-', '_');
            var a = (address ?? "addr").Trim().Replace('/', '_').Replace('.', '_').Replace(':', '_').Replace('-', '_');
            return $"tag_{c}_{a}";
        }

        public static string BuildAddressKey(string? connId, string? address)
        {
            return $"{(connId ?? string.Empty).Trim()}:{(address ?? string.Empty).Trim()}";
        }

        private static bool HasAnyDataSources(HmiConfiguration config)
        {
            return (config.Dashboard?.Widgets?.Any(w => !string.IsNullOrEmpty(w.Source?.Address)) ?? false) ||
                   (config.Mimic?.Widgets?.Any(w => !string.IsNullOrEmpty(w.Source?.Address)) ?? false);
        }
    }
}
