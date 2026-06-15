using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
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
                            var def = CreateDefaultConfiguration();
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
            var defaultConfig = CreateDefaultConfiguration();
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

        private List<WidgetConfig> CreateGpaChildren(int gpaNum)
        {
            return new List<WidgetConfig>
            {
                new WidgetConfig
                {
                    Type = "ValueDisplay",
                    Title = "Gen Active Power",
                    Source = new DataSourceConfig { ConnId = "mqtt1", Address = $"gMqt/GPA{gpaNum}/PPU_Gen_active_power", DataType = "Float32" },
                    Position = new WidgetPosition { Row = 0, Col = 0, SizeX = 1, SizeY = 1 },
                    Format = "{0:F0} kW"
                },
                new WidgetConfig
                {
                    Type = "ValueDisplay",
                    Title = "Gen Active Energy",
                    Source = new DataSourceConfig { ConnId = "mqtt1", Address = $"gMqt/GPA{gpaNum}/PPU_Gen_counter_active_power", DataType = "Float32" },
                    Position = new WidgetPosition { Row = 0, Col = 1, SizeX = 1, SizeY = 1 },
                    Format = "{0:F1} kWh"
                },
                new WidgetConfig
                {
                    Type = "ValueDisplay",
                    Title = "Exhaust Temp T404",
                    Source = new DataSourceConfig { ConnId = "mqtt1", Address = $"gMqt/GPA{gpaNum}/T404", DataType = "Float32" },
                    Position = new WidgetPosition { Row = 1, Col = 0, SizeX = 1, SizeY = 1 },
                    Format = "{0:F1} °C"
                },
                new WidgetConfig
                {
                    Type = "ValueDisplay",
                    Title = "Operating Hours",
                    Source = new DataSourceConfig { ConnId = "mqtt1", Address = $"gMqt/GPA{gpaNum}/Hours", DataType = "Float32" },
                    Position = new WidgetPosition { Row = 1, Col = 1, SizeX = 1, SizeY = 1 },
                    Format = "{0:F0} h"
                },
                new WidgetConfig
                {
                    Type = "PilotLight",
                    Title = "Status MWM",
                    Source = new DataSourceConfig { ConnId = "mqtt1", Address = $"gMqt/GPA{gpaNum}/Status_MWM", DataType = "Bool" },
                    Position = new WidgetPosition { Row = 0, Col = 2, SizeX = 1, SizeY = 1 },
                    TrueColor = "#00FF00",
                    FalseColor = "#440000"
                },
                new WidgetConfig
                {
                    Type = "PilotLight",
                    Title = "Word55 Warning",
                    Source = new DataSourceConfig { ConnId = "mqtt1", Address = $"gMqt/GPA{gpaNum}/HAS_IN_Word55_0", DataType = "Bool" },
                    Position = new WidgetPosition { Row = 1, Col = 2, SizeX = 1, SizeY = 1 },
                    TrueColor = "#FFCC00",
                    FalseColor = "#443300"
                },
                new WidgetConfig
                {
                    Type = "RealTimeTrend",
                    Title = $"GPA {gpaNum} Power Trend",
                    Source = new DataSourceConfig { ConnId = "mqtt1", Address = $"gMqt/GPA{gpaNum}/PPU_Gen_active_power", DataType = "Float32" },
                    Position = new WidgetPosition { Row = 2, Col = 0, SizeX = 3, SizeY = 2 },
                    MinValue = 0,
                    MaxValue = 2000
                }
            };
        }

        private HmiConfiguration CreateDefaultConfiguration()
        {
            return new HmiConfiguration
            {
                Project = new ProjectConfig { Name = "GPA Gas Pumping Station HMI", Version = "1.0.0" },
                Connections = new List<ConnectionConfig>
                {
                    new ConnectionConfig { Id = "plc1", Type = "ModbusTCP", Host = "127.0.0.1", Port = 502, PollIntervalMs = 500, ByteOrder = "ABCD" },
                    new ConnectionConfig { Id = "mqtt1", Type = "MQTT", Host = "stp10", Port = 1883, PollIntervalMs = 1000, ByteOrder = "ABCD" }
                },
                Dashboard = new DashboardConfig
                {
                    Widgets = new List<WidgetConfig>
                    {
                        new WidgetConfig
                        {
                            Type = "ValueDisplay",
                            Title = "ZAS Actual Power",
                            Source = new DataSourceConfig { ConnId = "mqtt1", Address = "gMqt/ZAS/ZAS_Actual_Power", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 0, Col = 0, SizeX = 1, SizeY = 1 },
                            Format = "{0:F1} kW"
                        },
                        new WidgetConfig
                        {
                            Type = "SetValue",
                            Title = "ZAS Power Setpoint",
                            Source = new DataSourceConfig { ConnId = "mqtt1", Address = "gMqt/ZAS/ZAS_Setpoint_Power", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 0, Col = 1, SizeX = 1, SizeY = 1 },
                            Format = "{0:F1} kW",
                            MinValue = 0,
                            MaxValue = 1000
                        },
                        new WidgetConfig
                        {
                            Type = "RealTimeTrend",
                            Title = "ZAS Power History",
                            Source = new DataSourceConfig { ConnId = "mqtt1", Address = "gMqt/ZAS/ZAS_Actual_Power", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 1, Col = 0, SizeX = 2, SizeY = 2 },
                            MinValue = 0,
                            MaxValue = 500
                        },
                        new WidgetConfig
                        {
                            Type = "ContainerButton",
                            Title = "GPA 1 Unit",
                            Position = new WidgetPosition { Row = 0, Col = 2, SizeX = 1, SizeY = 1 },
                            Children = CreateGpaChildren(1)
                        },
                        new WidgetConfig
                        {
                            Type = "ContainerButton",
                            Title = "GPA 2 Unit",
                            Position = new WidgetPosition { Row = 0, Col = 3, SizeX = 1, SizeY = 1 },
                            Children = CreateGpaChildren(2)
                        },
                        new WidgetConfig
                        {
                            Type = "ContainerButton",
                            Title = "GPA 3 Unit",
                            Position = new WidgetPosition { Row = 1, Col = 2, SizeX = 1, SizeY = 1 },
                            Children = CreateGpaChildren(3)
                        },
                        new WidgetConfig
                        {
                            Type = "ContainerButton",
                            Title = "GPA 4 Unit",
                            Position = new WidgetPosition { Row = 1, Col = 3, SizeX = 1, SizeY = 1 },
                            Children = CreateGpaChildren(4)
                        },
                        new WidgetConfig
                        {
                            Type = "ContainerButton",
                            Title = "GPA 5 Unit",
                            Position = new WidgetPosition { Row = 2, Col = 2, SizeX = 1, SizeY = 1 },
                            Children = CreateGpaChildren(5)
                        },
                        new WidgetConfig
                        {
                            Type = "ContainerButton",
                            Title = "GPA 6 Unit",
                            Position = new WidgetPosition { Row = 2, Col = 3, SizeX = 1, SizeY = 1 },
                            Children = CreateGpaChildren(6)
                        }
                    }
                },
                Mimic = new DashboardConfig
                {
                    CellSize = 10,
                    Widgets = new List<WidgetConfig>
                    {
                        new WidgetConfig
                        {
                            Type = "Tank",
                            Title = "B4305 Mixer Tank",
                            Source = new DataSourceConfig { ConnId = "mqtt1", Address = "gMqt/Mixer/Tank_Level", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 16, Col = 50, SizeX = 12, SizeY = 20 },
                            MinValue = 0,
                            MaxValue = 100,
                            Format = "{0:F1} %"
                        },
                        new WidgetConfig
                        {
                            Type = "Pipe",
                            Title = "CW Inlet 1",
                            Source = new DataSourceConfig { ConnId = "mqtt1", Address = "gMqt/Mixer/Pump_NS_Status", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 24, Col = 4, SizeX = 16, SizeY = 2 },
                            PipePoints = "0,1;15,1",
                            ActiveColor = "#00AAFF",
                            InactiveColor = "#005588",
                            ShowFlanges = true
                        },
                        new WidgetConfig
                        {
                            Type = "Pump",
                            Title = "NS_P4310 Pump",
                            Source = new DataSourceConfig { ConnId = "mqtt1", Address = "gMqt/Mixer/Pump_NS_Status", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 22, Col = 20, SizeX = 6, SizeY = 6 },
                            ActiveColor = "#00FF00",
                            InactiveColor = "#FF0000"
                        },
                        new WidgetConfig
                        {
                            Type = "Pipe",
                            Title = "CW Inlet 2",
                            Source = new DataSourceConfig { ConnId = "mqtt1", Address = "gMqt/Mixer/Pump_NS_Status", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 24, Col = 26, SizeX = 24, SizeY = 2 },
                            PipePoints = "0,1;23,1",
                            ActiveColor = "#00AAFF",
                            InactiveColor = "#005588",
                            ShowFlanges = true
                        },
                        new WidgetConfig
                        {
                            Type = "Valve",
                            Title = "YV_W1 Valve",
                            Source = new DataSourceConfig { ConnId = "mqtt1", Address = "gMqt/Mixer/Valve_YV_W1", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 22, Col = 36, SizeX = 6, SizeY = 6 },
                            ValveType = "CutOff",
                            ActiveColor = "#00FF00",
                            InactiveColor = "#FF0000",
                            ActuatorType = "Solenoid"
                        },
                        new WidgetConfig
                        {
                            Type = "Pipe",
                            Title = "Steam Inlet 1",
                            Source = new DataSourceConfig { ConnId = "mqtt1", Address = "gMqt/Mixer/Valve_YV_S1", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 10, Col = 4, SizeX = 28, SizeY = 10 },
                            PipePoints = "0,1;20,1;20,9;27,9",
                            ActiveColor = "#FF5500",
                            InactiveColor = "#882200",
                            ShowFlanges = true
                        },
                        new WidgetConfig
                        {
                            Type = "Valve",
                            Title = "YV_S1 Regulating",
                            Source = new DataSourceConfig { ConnId = "mqtt1", Address = "gMqt/Mixer/Valve_YV_S1", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 16, Col = 32, SizeX = 6, SizeY = 6 },
                            ValveType = "Regulating",
                            ActiveColor = "#00FF00",
                            InactiveColor = "#FF0000",
                            ActuatorType = "Diaphragm"
                        },
                        new WidgetConfig
                        {
                            Type = "Pipe",
                            Title = "Steam Inlet 2",
                            Source = new DataSourceConfig { ConnId = "mqtt1", Address = "gMqt/Mixer/Valve_YV_S1", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 18, Col = 38, SizeX = 12, SizeY = 2 },
                            PipePoints = "0,1;11,1",
                            ActiveColor = "#FF5500",
                            InactiveColor = "#882200",
                            ShowFlanges = true
                        },
                        new WidgetConfig
                        {
                            Type = "Pipe",
                            Title = "Mixed Outlet",
                            Source = new DataSourceConfig { ConnId = "mqtt1", Address = "gMqt/Mixer/Pump_NS_Status", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 26, Col = 62, SizeX = 24, SizeY = 10 },
                            PipePoints = "0,1;12,1;12,9;23,9",
                            ActiveColor = "#00FF99",
                            InactiveColor = "#007755",
                            ShowFlanges = true
                        },
                        new WidgetConfig
                        {
                            Type = "Valve",
                            Title = "YV_M1 Valve",
                            Source = new DataSourceConfig { ConnId = "mqtt1", Address = "gMqt/Mixer/Valve_YV_M1", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 24, Col = 68, SizeX = 6, SizeY = 6 },
                            ValveType = "CutOff",
                            ActiveColor = "#00FF00",
                            InactiveColor = "#FF0000",
                            ActuatorType = "Solenoid"
                        },
                        new WidgetConfig
                        {
                            Type = "ValueDisplay",
                            Title = "TE4305 Temp",
                            Source = new DataSourceConfig { ConnId = "mqtt1", Address = "gMqt/Mixer/Mixed_Temp", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 10, Col = 54, SizeX = 8, SizeY = 4 },
                            Format = "{0:F1} °C"
                        },
                        new WidgetConfig
                        {
                            Type = "ValueDisplay",
                            Title = "PT4305 Press",
                            Source = new DataSourceConfig { ConnId = "mqtt1", Address = "gMqt/Mixer/Tank_Pressure", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 10, Col = 40, SizeX = 8, SizeY = 4 },
                            Format = "{0:F2} bar"
                        }
                    }
                }
            };
        }
    }
}