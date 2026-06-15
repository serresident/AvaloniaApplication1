using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaApplication1.Models.Config;
using NModbus;
using MQTTnet;
using MQTTnet.Client;

namespace AvaloniaApplication1.Services
{
    public class DataCoreService : IDataCoreService, IMockDataService
    {
        private readonly IConfigurationService _configService;
        private readonly ConcurrentDictionary<string, object> _currentValues = new();
        private readonly List<Task> _pollingTasks = new();
        
        private CancellationTokenSource? _cts;
        private IMqttClient? _mqttClient;
        private HmiConfiguration? _config;
        private Random _random = new();
        private double _temperature = 20.0;

        public event EventHandler<(string ConnId, string Address, object Value)>? TagValueChanged;

        public DataCoreService(IConfigurationService configService)
        {
            _configService = configService;
        }

        // --- IDataCoreService / IMockDataService Start/Stop implementation ---

        public void Start() => StartSimulation();
        
        public void Stop() => StopSimulation();

        public void StartSimulation()
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();

            // 1. Initialize default/simulated values
            InitializeMockValues();

            // 2. Load configuration and start real Modbus/MQTT drivers
            Task.Run(async () =>
            {
                try
                {
                    _config = await _configService.LoadConfigurationAsync();
                    
                    // Start simulation loop (as fallback/mock)
                    _ = Task.Run(() => SimulationLoop(_cts.Token), _cts.Token);

                    // Start Modbus polling and MQTT connection
                    StartCommunicationDrivers(_cts.Token);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to start communications: {ex.Message}");
                }
            });
        }

        public void StopSimulation()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            _mqttClient?.Dispose();
            _mqttClient = null;

            _pollingTasks.Clear();
        }

        public object? GetCurrentValue(string connId, string address)
        {
            var key = $"{connId}_{address}";
            return _currentValues.TryGetValue(key, out var value) ? value : null;
        }

        public void WriteCommand(string connId, string address, object value)
        {
            var key = $"{connId}_{address}";
            _currentValues[key] = value;
            TagValueChanged?.Invoke(this, (connId, address, value));

            // Write to real connection asynchronously
            Task.Run(async () =>
            {
                try
                {
                    var connection = _config?.Connections?.FirstOrDefault(c => c.Id == connId);
                    if (connection != null)
                    {
                        if (connection.Type == "MQTT")
                        {
                            await WriteMqttCommandAsync(connection, address, value);
                        }
                        else if (connection.Type == "ModbusTCP" || connection.Type == "ModbusRTUOverTCP")
                        {
                            await WriteModbusCommandAsync(connection, address, value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to write command to {connId}/{address}: {ex.Message}");
                }
            });
        }

        // --- Mock Simulation ---

        private void InitializeMockValues()
        {
            _currentValues["plc1_40001"] = 20.0f; // Temperature
            _currentValues["plc1_00001"] = false; // Pump Status
            _currentValues["mqtt1_gMqt/ZAS/ZAS_Actual_Power"] = 0.0f;
            _currentValues["mqtt1_gMqt/ZAS/ZAS_Setpoint_Power"] = 0.0f;

            // Mixer Mimic Simulated Tags
            _currentValues["mqtt1_gMqt/Mixer/Tank_Level"] = 50.0f;
            _currentValues["mqtt1_gMqt/Mixer/Pump_NS_Status"] = true;
            _currentValues["mqtt1_gMqt/Mixer/Valve_YV_W1"] = true;
            _currentValues["mqtt1_gMqt/Mixer/Valve_YV_S1"] = 45.0f;
            _currentValues["mqtt1_gMqt/Mixer/Valve_YV_M1"] = true;
            _currentValues["mqtt1_gMqt/Mixer/Mixed_Temp"] = 60.0f;
            _currentValues["mqtt1_gMqt/Mixer/Tank_Pressure"] = 2.4f;

            for (int i = 1; i <= 6; i++)
            {
                _currentValues[$"mqtt1_gMqt/GPA{i}/PPU_Gen_active_power"] = 0.0f;
                _currentValues[$"mqtt1_gMqt/GPA{i}/PPU_Gen_counter_active_power"] = 0.0f;
                _currentValues[$"mqtt1_gMqt/GPA{i}/T404"] = 0.0f;
                _currentValues[$"mqtt1_gMqt/GPA{i}/Hours"] = 0.0f;
                _currentValues[$"mqtt1_gMqt/GPA{i}/Status_MWM"] = false;
                _currentValues[$"mqtt1_gMqt/GPA{i}/HAS_IN_Word55_0"] = false;
            }
        }

        private async Task SimulationLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(1000, ct);

                // Simulate Temperature fluctuating
                _temperature += (_random.NextDouble() - 0.5) * 2.0;
                if (_temperature < 0) _temperature = 0;
                if (_temperature > 100) _temperature = 100;
                UpdateValue("plc1", "40001", (float)_temperature);

                // ZAS Setpoint tracking
                var setpoint = GetCurrentValue("mqtt1", "gMqt/ZAS/ZAS_Setpoint_Power") is float s ? s : 200.0f;
                var currentPower = GetCurrentValue("mqtt1", "gMqt/ZAS/ZAS_Actual_Power") is float p ? p : 150.0f;
                currentPower += (setpoint - currentPower) * 0.1f + (float)(_random.NextDouble() - 0.5) * 4.0f;
                if (currentPower < 0) currentPower = 0;
                if (currentPower > 1000) currentPower = 1000;
                UpdateValue("mqtt1", "gMqt/ZAS/ZAS_Actual_Power", currentPower);

                // Mixer Mimic Simulation
                var pumpOn = GetCurrentValue("mqtt1", "gMqt/Mixer/Pump_NS_Status") is bool bp && bp;
                var vWater = GetCurrentValue("mqtt1", "gMqt/Mixer/Valve_YV_W1") is bool bw && bw;
                
                var rawSteamVal = GetCurrentValue("mqtt1", "gMqt/Mixer/Valve_YV_S1");
                float vSteam = 40.0f;
                if (rawSteamVal is float fs) vSteam = fs;
                else if (rawSteamVal is double ds) vSteam = (float)ds;
                else if (rawSteamVal is int ins) vSteam = ins;

                var vMixed = GetCurrentValue("mqtt1", "gMqt/Mixer/Valve_YV_M1") is bool bm && bm;

                // Tank Level
                var level = GetCurrentValue("mqtt1", "gMqt/Mixer/Tank_Level") is float lvl ? lvl : 50.0f;
                double levelDelta = 0;
                if (pumpOn && vWater) levelDelta += 1.5;
                if (vMixed) levelDelta -= 1.0;
                level += (float)levelDelta + (float)(_random.NextDouble() - 0.5) * 0.4f;
                level = Math.Clamp(level, 0.0f, 100.0f);
                UpdateValue("mqtt1", "gMqt/Mixer/Tank_Level", level);

                // Pressure
                var pressure = 1.0f + (level / 40.0f) + (float)(_random.NextDouble() - 0.5) * 0.1f;
                UpdateValue("mqtt1", "gMqt/Mixer/Tank_Pressure", pressure);

                // Temp
                double targetTemp = 20.0;
                if (vWater && vSteam > 0) targetTemp = 15.0 + (vSteam / 100.0) * 80.0;
                else if (vSteam > 0) targetTemp = 120.0;
                else if (vWater) targetTemp = 15.0;

                var currentTemp = GetCurrentValue("mqtt1", "gMqt/Mixer/Mixed_Temp") is float ctT ? ctT : 40.0f;
                currentTemp += (float)((targetTemp - currentTemp) * 0.15 + (_random.NextDouble() - 0.5) * 0.5);
                UpdateValue("mqtt1", "gMqt/Mixer/Mixed_Temp", currentTemp);

                // GPA 1 to 6 simulation
                for (int i = 1; i <= 6; i++)
                {
                    var activePower = GetCurrentValue("mqtt1", $"gMqt/GPA{i}/PPU_Gen_active_power") is float ap ? ap : 1200.0f;
                    activePower += (float)(_random.NextDouble() - 0.5) * 20.0f;
                    if (activePower < 0) activePower = 0;
                    if (activePower > 2000) activePower = 2000;
                    UpdateValue("mqtt1", $"gMqt/GPA{i}/PPU_Gen_active_power", activePower);

                    var counter = GetCurrentValue("mqtt1", $"gMqt/GPA{i}/PPU_Gen_counter_active_power") is float cVal ? cVal : 18000.0f;
                    counter += activePower / 3600.0f;
                    UpdateValue("mqtt1", $"gMqt/GPA{i}/PPU_Gen_counter_active_power", counter);

                    var temp = GetCurrentValue("mqtt1", $"gMqt/GPA{i}/T404") is float tVal ? tVal : 45.0f;
                    temp += (float)(_random.NextDouble() - 0.5) * 0.5f;
                    if (temp < 0) temp = 0;
                    if (temp > 150) temp = 150;
                    UpdateValue("mqtt1", $"gMqt/GPA{i}/T404", temp);

                    var hours = GetCurrentValue("mqtt1", $"gMqt/GPA{i}/Hours") is float hVal ? hVal : 90000.0f;
                    hours += 1.0f / 3600.0f;
                    UpdateValue("mqtt1", $"gMqt/GPA{i}/Hours", hours);

                    if (_random.Next(1, 100) == 50)
                    {
                        var alarm = GetCurrentValue("mqtt1", $"gMqt/GPA{i}/HAS_IN_Word55_0") is bool aBool && aBool;
                        UpdateValue("mqtt1", $"gMqt/GPA{i}/HAS_IN_Word55_0", !alarm);
                    }
                }
            }
        }

        private void UpdateValue(string connId, string address, object value)
        {
            var key = $"{connId}_{address}";
            _currentValues[key] = value;
            TagValueChanged?.Invoke(this, (connId, address, value));
        }

        // --- Modbus and MQTT Drivers ---

        private void StartCommunicationDrivers(CancellationToken ct)
        {
            if (_config == null) return;

            foreach (var conn in _config.Connections)
            {
                if (conn.Type == "MQTT")
                {
                    _ = Task.Run(() => ConnectMqttLoop(conn, ct), ct);
                }
                else if (conn.Type == "ModbusTCP" || conn.Type == "ModbusRTUOverTCP")
                {
                    _pollingTasks.Add(Task.Run(() => PollModbusLoop(conn, ct), ct));
                }
            }
        }

        // --- Modbus Polling Loop ---

        private async Task PollModbusLoop(ConnectionConfig conn, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                using var tcpClient = new TcpClient();
                try
                {
                    var connectTask = tcpClient.ConnectAsync(conn.Host, conn.Port);
                    var delayTask = Task.Delay(5000, ct); // 5s timeout
                    var finishedTask = await Task.WhenAny(connectTask, delayTask);
                    
                    if (finishedTask == delayTask || !tcpClient.Connected)
                    {
                        throw new SocketException((int)SocketError.TimedOut);
                    }

                    var factory = new ModbusFactory();
                    using var master = factory.CreateMaster(tcpClient);
                    master.Transport.ReadTimeout = 1000;
                    master.Transport.WriteTimeout = 1000;

                    Console.WriteLine($"Modbus connected to {conn.Id} ({conn.Host}:{conn.Port})");

                    while (!ct.IsCancellationRequested && tcpClient.Connected)
                    {
                        var startTime = DateTime.UtcNow;

                        // Scan config widgets to find registers to poll
                        var widgetsToPoll = GetWidgetsForConnection(conn.Id);
                        foreach (var w in widgetsToPoll)
                        {
                            try
                            {
                                if (ParseModbusAddress(w.Source.Address, out byte fc, out ushort rawAddr))
                                {
                                    ushort offset = GetModbusOffset(fc, rawAddr);
                                    byte slaveId = 1; // Default slave ID

                                    var value = ReadModbusValue(master, slaveId, fc, offset, w.Source.DataType, conn.ByteOrder);
                                    if (value != null)
                                    {
                                        UpdateValue(conn.Id, w.Source.Address, value);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // Single tag read failure doesn't disconnect immediately, but will print warning
                                Console.WriteLine($"Modbus read error on tag {w.Source.Address}: {ex.Message}");
                            }
                        }

                        // Delay for poll interval
                        var elapsed = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                        var sleepTime = Math.Max(50, conn.PollIntervalMs - elapsed);
                        await Task.Delay(sleepTime, ct);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Modbus connection/polling failed for {conn.Id}: {ex.Message}");
                    // Wait before reconnecting
                    await Task.Delay(5000, ct);
                }
            }
        }

        private List<WidgetConfig> GetWidgetsForConnection(string connId)
        {
            var list = new List<WidgetConfig>();
            if (_config == null) return list;

            void ScanWidgets(List<WidgetConfig> widgets)
            {
                foreach (var w in widgets)
                {
                    if (w.Source != null && w.Source.ConnId == connId)
                    {
                        list.Add(w);
                    }
                    if (w.Children != null && w.Children.Count > 0)
                    {
                        ScanWidgets(w.Children);
                    }
                }
            }

            ScanWidgets(_config.Dashboard.Widgets);
            return list;
        }

        private bool ParseModbusAddress(string address, out byte functionCode, out ushort startAddress)
        {
            functionCode = 3;
            startAddress = 0;

            if (string.IsNullOrEmpty(address))
                return false;

            if (address.StartsWith("0"))
            {
                functionCode = 1;
                return ushort.TryParse(address, out startAddress) && startAddress >= 1;
            }
            if (address.StartsWith("1"))
            {
                functionCode = 2;
                return ushort.TryParse(address, out startAddress) && startAddress >= 10001;
            }
            if (address.StartsWith("3"))
            {
                functionCode = 4;
                return ushort.TryParse(address, out startAddress) && startAddress >= 30001;
            }
            if (address.StartsWith("4"))
            {
                functionCode = 3;
                return ushort.TryParse(address, out startAddress) && startAddress >= 40001;
            }

            return ushort.TryParse(address, out startAddress);
        }

        private ushort GetModbusOffset(byte functionCode, ushort address)
        {
            switch (functionCode)
            {
                case 1: return (ushort)(address - 1);
                case 2: return (ushort)(address - 10001);
                case 3: return (ushort)(address - 40001);
                case 4: return (ushort)(address - 30001);
                default: return address;
            }
        }

        private object? ReadModbusValue(IModbusMaster master, byte slaveId, byte functionCode, ushort offset, string dataType, string byteOrder)
        {
            int regCount = (dataType == "Float32" || dataType == "Float" || dataType == "Int32" || dataType == "UInt32") ? 2 :
                           (dataType == "Double") ? 4 : 1;

            if (functionCode == 1) // Read Coils
            {
                bool[] coils = master.ReadCoils(slaveId, offset, 1);
                return coils[0];
            }
            else if (functionCode == 2) // Read Inputs
            {
                bool[] inputs = master.ReadInputs(slaveId, offset, 1);
                return inputs[0];
            }
            else if (functionCode == 4) // Read Input Registers
            {
                ushort[] regs = master.ReadInputRegisters(slaveId, offset, (ushort)regCount);
                return ParseRegisters(regs, dataType, byteOrder);
            }
            else // Read Holding Registers (fc 3)
            {
                ushort[] regs = master.ReadHoldingRegisters(slaveId, offset, (ushort)regCount);
                return ParseRegisters(regs, dataType, byteOrder);
            }
        }

        private static ushort SwapBytes(ushort value)
        {
            return (ushort)((value >> 8) | (value << 8));
        }

        private object ParseRegisters(ushort[] regs, string dataType, string byteOrder)
        {
            if (regs.Length == 1)
            {
                if (dataType == "Int16") return (short)regs[0];
                if (dataType == "Bool") return regs[0] > 0;
                return regs[0]; // UInt16 / default
            }

            if (regs.Length >= 2)
            {
                ushort r0 = regs[0];
                ushort r1 = regs[1];
                uint val = 0;

                if (byteOrder == "ABCD")
                {
                    val = ((uint)r0 << 16) | r1;
                }
                else if (byteOrder == "CDAB")
                {
                    val = ((uint)r1 << 16) | r0;
                }
                else if (byteOrder == "BADC")
                {
                    val = ((uint)SwapBytes(r0) << 16) | SwapBytes(r1);
                }
                else // DCBA
                {
                    val = ((uint)SwapBytes(r1) << 16) | SwapBytes(r0);
                }

                if (dataType == "Float32" || dataType == "Float")
                {
                    return BitConverter.UInt32BitsToSingle(val);
                }
                if (dataType == "Int32")
                {
                    return (int)val;
                }
                if (dataType == "UInt32")
                {
                    return val;
                }
            }

            return regs[0];
        }

        private async Task WriteModbusCommandAsync(ConnectionConfig conn, string address, object value)
        {
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(conn.Host, conn.Port);

            var factory = new ModbusFactory();
            using var master = factory.CreateMaster(tcpClient);

            if (ParseModbusAddress(address, out byte fc, out ushort rawAddr))
            {
                ushort offset = GetModbusOffset(fc, rawAddr);
                byte slaveId = 1;

                if (fc == 1 || fc == 2)
                {
                    bool bVal = value is bool b ? b : (double.TryParse(value.ToString(), out double num) && num > 0);
                    await master.WriteSingleCoilAsync(slaveId, offset, bVal);
                }
                else
                {
                    double dVal = double.Parse(value.ToString() ?? "0");

                    var widgets = GetWidgetsForConnection(conn.Id);
                    var widget = widgets.FirstOrDefault(w => w.Source.Address == address);
                    string dataType = widget?.Source?.DataType ?? "Float32";

                    if (dataType == "Float32" || dataType == "Float" || dataType == "Int32" || dataType == "UInt32")
                    {
                        uint bits = 0;
                        if (dataType == "Float32" || dataType == "Float")
                            bits = BitConverter.SingleToUInt32Bits((float)dVal);
                        else if (dataType == "Int32")
                            bits = (uint)(int)dVal;
                        else
                            bits = (uint)dVal;

                        ushort r0 = 0, r1 = 0;
                        if (conn.ByteOrder == "ABCD")
                        {
                            r0 = (ushort)(bits >> 16);
                            r1 = (ushort)(bits & 0xFFFF);
                        }
                        else if (conn.ByteOrder == "CDAB")
                        {
                            r1 = (ushort)(bits >> 16);
                            r0 = (ushort)(bits & 0xFFFF);
                        }
                        else if (conn.ByteOrder == "BADC")
                        {
                            r0 = SwapBytes((ushort)(bits >> 16));
                            r1 = SwapBytes((ushort)(bits & 0xFFFF));
                        }
                        else // DCBA
                        {
                            r1 = SwapBytes((ushort)(bits >> 16));
                            r0 = SwapBytes((ushort)(bits & 0xFFFF));
                        }

                        await master.WriteMultipleRegistersAsync(slaveId, offset, new ushort[] { r0, r1 });
                    }
                    else // 16-bit register
                    {
                        ushort uReg = (ushort)(short)dVal;
                        await master.WriteSingleRegisterAsync(slaveId, offset, uReg);
                    }
                }
            }
        }

        // --- MQTT Connection Loop ---

        private async Task ConnectMqttLoop(ConnectionConfig conn, CancellationToken ct)
        {
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(conn.Host, conn.Port)
                .WithCleanSession()
                .Build();

            _mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                string topic = e.ApplicationMessage.Topic;
                string payload = System.Text.Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

                var widgets = GetWidgetsForConnection(conn.Id);
                var widget = widgets.FirstOrDefault(w => w.Source.Address == topic);
                string dataType = widget?.Source?.DataType ?? "Float32";

                object parsedVal = payload;
                if (dataType == "Bool")
                {
                    parsedVal = payload == "1" || payload.ToLower() == "true";
                }
                else if (dataType == "Float32" || dataType == "Float" || dataType == "Double")
                {
                    if (float.TryParse(payload, out float fVal)) parsedVal = fVal;
                }
                else
                {
                    if (int.TryParse(payload, out int iVal)) parsedVal = iVal;
                }

                UpdateValue(conn.Id, topic, parsedVal);
                return Task.CompletedTask;
            };

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (!_mqttClient.IsConnected)
                    {
                        Console.WriteLine($"MQTT connecting to {conn.Host}:{conn.Port}...");
                        await _mqttClient.ConnectAsync(options, ct);
                        Console.WriteLine("MQTT connected!");

                        var widgets = GetWidgetsForConnection(conn.Id);
                        foreach (var w in widgets)
                        {
                            await _mqttClient.SubscribeAsync(w.Source.Address);
                            Console.WriteLine($"MQTT Subscribed to: {w.Source.Address}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"MQTT connection failed: {ex.Message}");
                }

                await Task.Delay(5000, ct);
            }
        }

        private async Task WriteMqttCommandAsync(ConnectionConfig conn, string address, object value)
        {
            if (_mqttClient == null || !_mqttClient.IsConnected) return;

            string payload = value.ToString() ?? "";
            if (value is bool b)
            {
                payload = b ? "true" : "false";
            }

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(address)
                .WithPayload(payload)
                .WithRetainFlag(true)
                .Build();

            await _mqttClient.PublishAsync(message);
        }
    }
}
