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
                new ValueDisplayConfig
                        {
                            Type = "ValueDisplay",
                    Title = "Gen Active Power",
                    Source = new DataSourceConfig { ConnId = "mqtt1", Address = $"gMqt/GPA{gpaNum}/PPU_Gen_active_power", DataType = "Float32" },
                    Position = new WidgetPosition { Row = 0, Col = 0, SizeX = 1, SizeY = 1 },
                    Format = "{0:F0} kW"
                },
                new ValueDisplayConfig
                        {
                            Type = "ValueDisplay",
                    Title = "Gen Active Energy",
                    Source = new DataSourceConfig { ConnId = "mqtt1", Address = $"gMqt/GPA{gpaNum}/PPU_Gen_counter_active_power", DataType = "Float32" },
                    Position = new WidgetPosition { Row = 0, Col = 1, SizeX = 1, SizeY = 1 },
                    Format = "{0:F1} kWh"
                },
                new ValueDisplayConfig
                        {
                            Type = "ValueDisplay",
                    Title = "Exhaust Temp T404",
                    Source = new DataSourceConfig { ConnId = "mqtt1", Address = $"gMqt/GPA{gpaNum}/T404", DataType = "Float32" },
                    Position = new WidgetPosition { Row = 1, Col = 0, SizeX = 1, SizeY = 1 },
                    Format = "{0:F1} °C"
                },
                new ValueDisplayConfig
                        {
                            Type = "ValueDisplay",
                    Title = "Operating Hours",
                    Source = new DataSourceConfig { ConnId = "mqtt1", Address = $"gMqt/GPA{gpaNum}/Hours", DataType = "Float32" },
                    Position = new WidgetPosition { Row = 1, Col = 1, SizeX = 1, SizeY = 1 },
                    Format = "{0:F0} h"
                },
                new PilotLightConfig
                        {
                            Type = "PilotLight",
                    Title = "Status MWM",
                    Source = new DataSourceConfig { ConnId = "mqtt1", Address = $"gMqt/GPA{gpaNum}/Status_MWM", DataType = "Bool" },
                    Position = new WidgetPosition { Row = 0, Col = 2, SizeX = 1, SizeY = 1 },
                    TrueColor = "#00FF00",
                    FalseColor = "#440000"
                },
                new PilotLightConfig
                        {
                            Type = "PilotLight",
                    Title = "Word55 Warning",
                    Source = new DataSourceConfig { ConnId = "mqtt1", Address = $"gMqt/GPA{gpaNum}/HAS_IN_Word55_0", DataType = "Bool" },
                    Position = new WidgetPosition { Row = 1, Col = 2, SizeX = 1, SizeY = 1 },
                    TrueColor = "#FFCC00",
                    FalseColor = "#443300"
                },
                new RealTimeTrendConfig
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
                        new ValueDisplayConfig
                        {
                            Type = "ValueDisplay",
                            Title = "ZAS Actual Power",
                            Source = new DataSourceConfig { ConnId = "mqtt1", Address = "gMqt/ZAS/ZAS_Actual_Power", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 0, Col = 0, SizeX = 1, SizeY = 1 },
                            Format = "{0:F1} kW"
                        },
                        new SetValueConfig
                        {
                            Type = "SetValue",
                            Title = "ZAS Power Setpoint",
                            Source = new DataSourceConfig { ConnId = "mqtt1", Address = "gMqt/ZAS/ZAS_Setpoint_Power", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 0, Col = 1, SizeX = 1, SizeY = 1 },
                            Format = "{0:F1} kW",
                            MinValue = 0,
                            MaxValue = 1000
                        },
                        new RealTimeTrendConfig
                        {
                            Type = "RealTimeTrend",
                            Title = "ZAS Power History",
                            Source = new DataSourceConfig { ConnId = "mqtt1", Address = "gMqt/ZAS/ZAS_Actual_Power", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 1, Col = 0, SizeX = 2, SizeY = 2 },
                            MinValue = 0,
                            MaxValue = 500
                        },
                        new ContainerButtonConfig
                        {
                            Type = "ContainerButton",
                            Title = "GPA 1 Unit",
                            Position = new WidgetPosition { Row = 0, Col = 2, SizeX = 1, SizeY = 1 },
                            Children = CreateGpaChildren(1)
                        },
                        new ContainerButtonConfig
                        {
                            Type = "ContainerButton",
                            Title = "GPA 2 Unit",
                            Position = new WidgetPosition { Row = 0, Col = 3, SizeX = 1, SizeY = 1 },
                            Children = CreateGpaChildren(2)
                        },
                        new ContainerButtonConfig
                        {
                            Type = "ContainerButton",
                            Title = "GPA 3 Unit",
                            Position = new WidgetPosition { Row = 1, Col = 2, SizeX = 1, SizeY = 1 },
                            Children = CreateGpaChildren(3)
                        },
                        new ContainerButtonConfig
                        {
                            Type = "ContainerButton",
                            Title = "GPA 4 Unit",
                            Position = new WidgetPosition { Row = 1, Col = 3, SizeX = 1, SizeY = 1 },
                            Children = CreateGpaChildren(4)
                        },
                        new ContainerButtonConfig
                        {
                            Type = "ContainerButton",
                            Title = "GPA 5 Unit",
                            Position = new WidgetPosition { Row = 2, Col = 2, SizeX = 1, SizeY = 1 },
                            Children = CreateGpaChildren(5)
                        },
                        new ContainerButtonConfig
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
                        // --- WATER SUPPLY ---
                        new PumpConfig
                        {
                            Type = "Pump",
                            Title = "Насос воды NS1",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "00001", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 4.5, Col = 9.5, SizeX = 6, SizeY = 6 },
                            ActiveColor = "#00FF00",
                            InactiveColor = "#FF0000"
                        },
                        new ValveConfig
                        {
                            Type = "Valve",
                            Title = "Клапан воды YV1",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "00002", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 4.5, Col = 23.5, SizeX = 6, SizeY = 6 },
                            ValveType = "CutOff",
                            ActiveColor = "#00FF00",
                            InactiveColor = "#FF0000",
                            ActuatorType = "Solenoid",
                            FeedbackSource = new DataSourceConfig { ConnId = "plc1", Address = "10002", DataType = "Bool" }
                        },
                        new PipeConfig
                        {
                            Type = "Pipe",
                            Title = "Линия воды 1",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "00001", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 6, Col = 4, SizeX = 6, SizeY = 2 },
                            PipePoints = "0,1;5,1",
                            ActiveColor = "#00AAFF",
                            InactiveColor = "#005588",
                            ShowFlanges = true
                        },
                        new PipeConfig
                        {
                            Type = "Pipe",
                            Title = "Линия воды 2",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "00001", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 6, Col = 15, SizeX = 9, SizeY = 2 },
                            PipePoints = "0,1;8,1",
                            ActiveColor = "#00AAFF",
                            InactiveColor = "#005588",
                            ShowFlanges = true
                        },
                        new PipeConfig
                        {
                            Type = "Pipe",
                            Title = "Линия воды в бак",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "00001", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 6, Col = 29, SizeX = 21, SizeY = 16 },
                            PipePoints = "0,1;17,1;17,15",
                            ActiveColor = "#00AAFF",
                            InactiveColor = "#005588",
                            ShowFlanges = true
                        },
                        new ValueDisplayConfig
                        {
                            Type = "ValueDisplay",
                            Title = "Дозатор воды",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "30035", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 12, Col = 10, SizeX = 8, SizeY = 4 },
                            Format = "{0:F1} л"
                        },
                        new SetValueConfig
                        {
                            Type = "SetValue",
                            Title = "Уставка воды",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "40005", DataType = "Int16" },
                            Position = new WidgetPosition { Row = 12, Col = 19, SizeX = 8, SizeY = 4 },
                            Format = "{0} л",
                            MinValue = 0,
                            MaxValue = 1000
                        },
                        new CommandButtonConfig
                        {
                            Type = "CommandButton",
                            Title = "Сбросить дозу",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "00010", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 12, Col = 28, SizeX = 8, SizeY = 4 },
                            ButtonMode = "Pulse"
                        },

                        // --- FEEDER A ---
                        new PumpConfig
                        {
                            Type = "Pump",
                            Title = "Питатель A QS1",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "00003", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 24.5, Col = 9.5, SizeX = 6, SizeY = 6 },
                            ActiveColor = "#00FF00",
                            InactiveColor = "#FF0000"
                        },
                        new PipeConfig
                        {
                            Type = "Pipe",
                            Title = "Линия подачи A",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "00003", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 26, Col = 15, SizeX = 31, SizeY = 2 },
                            PipePoints = "0,1;30,1",
                            ActiveColor = "#E5C158",
                            InactiveColor = "#7F6B2F",
                            ShowFlanges = true
                        },
                        new ValueDisplayConfig
                        {
                            Type = "ValueDisplay",
                            Title = "Вес A в смесители",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "30031", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 31, Col = 10, SizeX = 8, SizeY = 4 },
                            Format = "{0:F1} кг"
                        },
                        new SetValueConfig
                        {
                            Type = "SetValue",
                            Title = "Уставка веса A",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "40006", DataType = "Int16" },
                            Position = new WidgetPosition { Row = 31, Col = 19, SizeX = 8, SizeY = 4 },
                            Format = "{0} кг",
                            MinValue = 0,
                            MaxValue = 500
                        },

                        // --- FEEDER B ---
                        new PumpConfig
                        {
                            Type = "Pump",
                            Title = "Питатель B QS2",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "00004", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 38.5, Col = 9.5, SizeX = 6, SizeY = 6 },
                            ActiveColor = "#00FF00",
                            InactiveColor = "#FF0000"
                        },
                        new PipeConfig
                        {
                            Type = "Pipe",
                            Title = "Линия подачи B",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "00004", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 40, Col = 15, SizeX = 31, SizeY = 2 },
                            PipePoints = "0,1;30,1",
                            ActiveColor = "#BC7FEB",
                            InactiveColor = "#613880",
                            ShowFlanges = true
                        },
                        new ValueDisplayConfig
                        {
                            Type = "ValueDisplay",
                            Title = "Вес B в смесители",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "30033", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 45, Col = 10, SizeX = 8, SizeY = 4 },
                            Format = "{0:F1} кг"
                        },
                        new SetValueConfig
                        {
                            Type = "SetValue",
                            Title = "Уставка веса B",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "40007", DataType = "Int16" },
                            Position = new WidgetPosition { Row = 45, Col = 19, SizeX = 8, SizeY = 4 },
                            Format = "{0} кг",
                            MinValue = 0,
                            MaxValue = 500
                        },

                        // --- MAIN VESSEL ---
                        new TankConfig
                        {
                            Type = "Tank",
                            Title = "Реактор смеситель",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "30029", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 20, Col = 46, SizeX = 16, SizeY = 24 },
                            MinValue = 0,
                            MaxValue = 1000,
                            Format = "{0:F1} кг"
                        },
                        new ValueDisplayConfig
                        {
                            Type = "ValueDisplay",
                            Title = "Давление реактора",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "30025", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 15, Col = 46, SizeX = 8, SizeY = 4 },
                            Format = "{0:F2} бар"
                        },
                        new ValueDisplayConfig
                        {
                            Type = "ValueDisplay",
                            Title = "Температура смеси",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "30023", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 15, Col = 54, SizeX = 8, SizeY = 4 },
                            Format = "{0:F1} °C"
                        },

                        // --- TEMP CONTROL ---
                        new CommandButtonConfig
                        {
                            Type = "CommandButton",
                            Title = "Нагрев АВТО",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "00011", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 4, Col = 68, SizeX = 8, SizeY = 4 },
                            ButtonMode = "Toggle"
                        },
                        new CommandButtonConfig
                        {
                            Type = "CommandButton",
                            Title = "Включить ТЭН",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "00006", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 4, Col = 77, SizeX = 8, SizeY = 4 },
                            ButtonMode = "Toggle"
                        },
                        new SetValueConfig
                        {
                            Type = "SetValue",
                            Title = "Уставка темп-ры",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "40033", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 9, Col = 68, SizeX = 8, SizeY = 4 },
                            Format = "{0:F1} °C",
                            MinValue = 0,
                            MaxValue = 100
                        },
                        new ValueDisplayConfig
                        {
                            Type = "ValueDisplay",
                            Title = "Мощность ТЭНа",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "40025", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 9, Col = 77, SizeX = 8, SizeY = 4 },
                            Format = "{0:F1} %"
                        },

                        // --- COOLING LOOP ---
                        new PumpConfig
                        {
                            Type = "Pump",
                            Title = "Насос охлаждения",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "00007", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 15.5, Col = 67.5, SizeX = 6, SizeY = 6 },
                            ActiveColor = "#00FF00",
                            InactiveColor = "#FF0000"
                        },
                        new ValveConfig
                        {
                            Type = "Valve",
                            Title = "Клапан охл.",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "40027", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 15.5, Col = 77.5, SizeX = 6, SizeY = 6 },
                            ValveType = "Regulating",
                            ActiveColor = "#00FF00",
                            InactiveColor = "#FF0000",
                            ActuatorType = "Diaphragm",
                            FeedbackSource = new DataSourceConfig { ConnId = "plc1", Address = "40027", DataType = "Float32" }
                        },
                        new PipeConfig
                        {
                            Type = "Pipe",
                            Title = "Линия охл. 1",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "00007", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 17, Col = 62, SizeX = 6, SizeY = 2 },
                            PipePoints = "0,1;5,1",
                            ActiveColor = "#00AAFF",
                            InactiveColor = "#005588",
                            ShowFlanges = true
                        },
                        new PipeConfig
                        {
                            Type = "Pipe",
                            Title = "Линия охл. 2",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "00007", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 17, Col = 73, SizeX = 5, SizeY = 2 },
                            PipePoints = "0,1;4,1",
                            ActiveColor = "#00AAFF",
                            InactiveColor = "#005588",
                            ShowFlanges = true
                        },
                        new PipeConfig
                        {
                            Type = "Pipe",
                            Title = "Линия охл. 3",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "00007", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 17, Col = 83, SizeX = 7, SizeY = 2 },
                            PipePoints = "0,1;6,1",
                            ActiveColor = "#00AAFF",
                            InactiveColor = "#005588",
                            ShowFlanges = true
                        },

                        // --- PRESSURE CONTROL ---
                        new ValveConfig
                        {
                            Type = "Valve",
                            Title = "Сдув давления",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "00005", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 22.5, Col = 67.5, SizeX = 6, SizeY = 6 },
                            ValveType = "CutOff",
                            ActiveColor = "#00FF00",
                            InactiveColor = "#FF0000",
                            ActuatorType = "Solenoid",
                            FeedbackSource = new DataSourceConfig { ConnId = "plc1", Address = "10005", DataType = "Bool" }
                        },
                        new PipeConfig
                        {
                            Type = "Pipe",
                            Title = "Линия сдува",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "00005", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 24, Col = 62, SizeX = 6, SizeY = 2 },
                            PipePoints = "0,1;5,1",
                            ActiveColor = "#00AAFF",
                            InactiveColor = "#005588",
                            ShowFlanges = true
                        },
                        new SetValueConfig
                        {
                            Type = "SetValue",
                            Title = "Уставка давл.",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "40029", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 29, Col = 68, SizeX = 8, SizeY = 4 },
                            Format = "{0:F2} бар",
                            MinValue = 1,
                            MaxValue = 6
                        },
                        new SetValueConfig
                        {
                            Type = "SetValue",
                            Title = "Уставка сдува %",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "40021", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 29, Col = 77, SizeX = 8, SizeY = 4 },
                            Format = "{0:F1} %",
                            MinValue = 0,
                            MaxValue = 100
                        },

                        // --- DISCHARGE OUTLET ---
                        new PipeConfig
                        {
                            Type = "Pipe",
                            Title = "Линия слива",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "00001", DataType = "Bool" },
                            Position = new WidgetPosition { Row = 44, Col = 54, SizeX = 14, SizeY = 10 },
                            PipePoints = "0,1;0,9;13,9",
                            ActiveColor = "#00FF99",
                            InactiveColor = "#007755",
                            ShowFlanges = true
                        },
                        new ValueDisplayConfig
                        {
                            Type = "ValueDisplay",
                            Title = "Скорость слива",
                            Source = new DataSourceConfig { ConnId = "plc1", Address = "30027", DataType = "Float32" },
                            Position = new WidgetPosition { Row = 45, Col = 68, SizeX = 8, SizeY = 4 },
                            Format = "{0:F1} л/мин"
                        }
                    }
                }
            };
        }
    }
}