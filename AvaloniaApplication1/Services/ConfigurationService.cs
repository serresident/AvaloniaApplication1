using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Platform;
using AvaloniaApplication1.Models.Config;

namespace AvaloniaApplication1.Services
{
    public class ConfigurationService : IConfigurationService
    {
        private string _configFilePath = "config.json";
        public string CurrentFilePath
        {
            get => _configFilePath;
            set => _configFilePath = string.IsNullOrWhiteSpace(value) ? "config.json" : value;
        }

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public async Task<HmiConfiguration> LoadConfigurationAsync(string? filePath = null)
        {
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                CurrentFilePath = filePath;
            }

            if (File.Exists(CurrentFilePath))
            {
                try
                {
                    using var stream = File.OpenRead(CurrentFilePath);
                    var config = await JsonSerializer.DeserializeAsync<HmiConfiguration>(stream, _jsonOptions);
                    if (config != null)
                    {
                        if (config.Mimic == null || config.Mimic.Widgets == null || config.Mimic.Widgets.Count == 0 || config.Mimic.CellSize == 20)
                        {
                            var def = await GetDefaultConfigurationAsync();
                            config.Mimic = def.Mimic;
                            await SaveConfigurationAsync(config, CurrentFilePath);
                        }
                        return config;
                    }
                }
                catch (Exception ex)
                {
                    // For now, write to console. In a real app, log this error.
                    Console.WriteLine($"Failed to load configuration from {CurrentFilePath}: {ex.Message}");
                }
            }

            // Fallback to default if file doesn't exist or is invalid
            var defaultConfig = await GetDefaultConfigurationAsync();
            await SaveConfigurationAsync(defaultConfig, CurrentFilePath);
            return defaultConfig;
        }

        public async Task SaveConfigurationAsync(HmiConfiguration config, string? filePath = null)
        {
            var targetPath = !string.IsNullOrWhiteSpace(filePath) ? filePath : CurrentFilePath;
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                CurrentFilePath = filePath;
            }

            try
            {
                var dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                using var stream = File.Create(targetPath);
                await JsonSerializer.SerializeAsync(stream, config, _jsonOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save configuration to {targetPath}: {ex.Message}");
            }
        }

        private async Task<HmiConfiguration> GetDefaultConfigurationAsync()
        {
            try
            {
                var uri = new Uri("avares://AvaloniaApplication1/Assets/default_config.json");
                using var stream = AssetLoader.Open(uri);
                var config = await JsonSerializer.DeserializeAsync<HmiConfiguration>(stream, _jsonOptions);
                return config ?? new HmiConfiguration();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load default configuration from assets: {ex.Message}");
                return new HmiConfiguration();
            }
        }
    }
}