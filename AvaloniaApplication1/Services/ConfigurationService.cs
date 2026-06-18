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
        private readonly string _configFilePath = "config.json";
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public async Task<HmiConfiguration> LoadConfigurationAsync()
        {
            if (File.Exists(_configFilePath))
            {
                try
                {
                    using var stream = File.OpenRead(_configFilePath);
                    var config = await JsonSerializer.DeserializeAsync<HmiConfiguration>(stream, _jsonOptions);
                    if (config != null)
                    {
                        if (config.Mimic == null || config.Mimic.Widgets == null || config.Mimic.Widgets.Count == 0 || config.Mimic.CellSize == 20)
                        {
                            var def = await GetDefaultConfigurationAsync();
                            config.Mimic = def.Mimic;
                            await SaveConfigurationAsync(config);
                        }
                        return config;
                    }
                }
                catch (Exception ex)
                {
                    // For now, write to console. In a real app, log this error.
                    Console.WriteLine($"Failed to load configuration: {ex.Message}");
                }
            }

            // Fallback to default if file doesn't exist or is invalid
            var defaultConfig = await GetDefaultConfigurationAsync();
            await SaveConfigurationAsync(defaultConfig);
            return defaultConfig;
        }

        public async Task SaveConfigurationAsync(HmiConfiguration config)
        {
            try
            {
                using var stream = File.Create(_configFilePath);
                await JsonSerializer.SerializeAsync(stream, config, _jsonOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save configuration: {ex.Message}");
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