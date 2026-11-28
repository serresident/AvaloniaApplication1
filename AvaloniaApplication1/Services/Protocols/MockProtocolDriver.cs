using System;
using System.Globalization;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaApplication1.Models;

namespace AvaloniaApplication1.Services.Protocols
{
    public class MockProtocolDriver : IProtocolDriver
    {
        public string ConnectionId { get; }
        public bool IsConnected { get; private set; } = true;
        private readonly Subject<TagData> _tagUpdates = new();
        private readonly Random _random = new();
        private readonly SimulationEngine _simulationEngine = new();
        private ModbusServerManager? _modbusServerManager;

        public IObservable<TagData> TagUpdates => _tagUpdates;

        // Maintain some internal state for the mock
        private float _zasActualPower = 150.0f;
        private float _zasSetpointPower = 200.0f;

        // Closed-loop Demo Line State (demo/*)
        private double _demoT101Level = 720.0;
        private double _demoR101Level = 240.0;
        private double _demoT102Level = 120.0;
        private double _demoR101Temp = 48.0;
        private double _demoR101TempSp = 65.0;
        private bool _demoR101HeaterEnable = true;
        private bool _demoR101AgitatorCmd = true;
        private bool _demoR101AgitatorFb = true;
        private bool _demoR101ModeAuto = false;
        private bool _demoP101Cmd = true;
        private bool _demoP101Fb = true;
        private bool _demoP101ModeAuto = false;
        private bool _demoYV101Cmd = true;
        private bool _demoYV101OpenFb = true;
        private bool _demoYV101CloseFb = false;
        private bool _demoYV101ModeAuto = false;
        private bool _demoP102Cmd = true;
        private bool _demoP102Fb = true;
        private bool _demoP102ModeAuto = false;
        private double _demoP102FreqSp = 35.0;
        private double _demoP102FreqFb = 35.0;
        private double _demoFV102Pos = 60.0;
        private bool _demoYV103Cmd = false;
        private bool _demoLinePower = true;
        private bool _demoFaultActive = false;
        private int _demoTransitTick = 0;
        
        public MockProtocolDriver(string connectionId)
        {
            ConnectionId = connectionId;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // 1. Send initial mock states so widgets render correctly on startup
            EmitInitialStates();

            // 2. Start internal Modbus Server if needed by SimulationEngine
            _modbusServerManager = new ModbusServerManager();
            try
            {
                _modbusServerManager.Start(502, 1);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MockProtocolDriver caught ModbusServerManager Start error: {ex.Message}");
                // Port might be in use, ignore for pure mock
            }

            // 3. Start background mock loop
            Task.Run(() => MockLoop(cancellationToken), cancellationToken).FireAndForget(context: "MockProtocolDriver");

            return Task.CompletedTask;
        }

        public Task WriteAsync(string address, object value, CancellationToken cancellationToken)
        {
            if (address.StartsWith("demo/", StringComparison.OrdinalIgnoreCase))
            {
                ApplyDemoWrite(address, value);
                return Task.CompletedTask;
            }

            // In mock mode, writing simply echoes the value back as the new state
            if (address == "gMqt/ZAS/ZAS_Setpoint_Power" && float.TryParse(value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float fVal))
            {
                _zasSetpointPower = fVal;
            }

            _tagUpdates.OnNext(new TagData(ConnectionId, address, value ?? new object()));
            return Task.CompletedTask;
        }

        private void ApplyDemoWrite(string address, object value)
        {
            switch (address)
            {
                case "demo/P101/Cmd": _demoP101Cmd = TagValueConverter.ToBool(value); break;
                case "demo/P101/Mode": _demoP101ModeAuto = TagValueConverter.ToBool(value); break;
                case "demo/YV101/Cmd": _demoYV101Cmd = TagValueConverter.ToBool(value); break;
                case "demo/YV101/Mode": _demoYV101ModeAuto = TagValueConverter.ToBool(value); break;
                case "demo/R101/AgitatorCmd": _demoR101AgitatorCmd = TagValueConverter.ToBool(value); break;
                case "demo/R101/Mode": _demoR101ModeAuto = TagValueConverter.ToBool(value); break;
                case "demo/R101/TempSp": _demoR101TempSp = Math.Clamp(TagValueConverter.ToDouble(value), 10.0, 160.0); break;
                case "demo/R101/HeaterEnable": _demoR101HeaterEnable = TagValueConverter.ToBool(value); break;
                case "demo/P102/Cmd": _demoP102Cmd = TagValueConverter.ToBool(value); break;
                case "demo/P102/Mode": _demoP102ModeAuto = TagValueConverter.ToBool(value); break;
                case "demo/P102/FreqSp": _demoP102FreqSp = Math.Clamp(TagValueConverter.ToDouble(value), 0.0, 50.0); break;
                case "demo/FV102/Pos": _demoFV102Pos = Math.Clamp(TagValueConverter.ToDouble(value), 0.0, 100.0); break;
                case "demo/YV103/Cmd": _demoYV103Cmd = TagValueConverter.ToBool(value); break;
                case "demo/LinePower": _demoLinePower = TagValueConverter.ToBool(value); break;
                case "demo/ResetFault":
                    if (TagValueConverter.ToBool(value))
                    {
                        _demoFaultActive = false;
                        if (_demoP102FreqSp > 48.0) _demoP102FreqSp = 40.0;
                    }
                    break;
            }
            EmitDemoStates();
        }

        public Task StopAsync()
        {
            _modbusServerManager?.Stop();
            return Task.CompletedTask;
        }

        public void ResetSimulation()
        {
            _simulationEngine.ResetTotalizer = true;
            // Optionally reset internal mock state
            _zasActualPower = 150.0f;
            _zasSetpointPower = 200.0f;
        }

        public void Dispose()
        {
            _tagUpdates.Dispose();
            _modbusServerManager?.Stop();
        }

        private void EmitInitialStates()
        {
            _tagUpdates.OnNext(new TagData(ConnectionId, "plc1_40001", 20.0f));
            _tagUpdates.OnNext(new TagData(ConnectionId, "plc1_00001", false));
            _tagUpdates.OnNext(new TagData(ConnectionId, "gMqt/ZAS/ZAS_Actual_Power", _zasActualPower));
            _tagUpdates.OnNext(new TagData(ConnectionId, "gMqt/ZAS/ZAS_Setpoint_Power", _zasSetpointPower));
            _tagUpdates.OnNext(new TagData(ConnectionId, "gMqt/Mixer/Tank_Level", 50.0f));
            _tagUpdates.OnNext(new TagData(ConnectionId, "gMqt/Mixer/Pump_NS_Status", true));
            _tagUpdates.OnNext(new TagData(ConnectionId, "gMqt/Mixer/Valve_YV_W1", true));
            _tagUpdates.OnNext(new TagData(ConnectionId, "gMqt/Mixer/Valve_YV_S1", 45.0f));
            _tagUpdates.OnNext(new TagData(ConnectionId, "gMqt/Mixer/Valve_YV_M1", true));
            _tagUpdates.OnNext(new TagData(ConnectionId, "gMqt/Mixer/Mixed_Temp", 60.0f));
            _tagUpdates.OnNext(new TagData(ConnectionId, "gMqt/Mixer/Tank_Pressure", 2.4f));

            EmitDemoStates();

            for (int i = 1; i <= 6; i++)
            {
                _tagUpdates.OnNext(new TagData(ConnectionId, $"gMqt/GPA{i}/PPU_Gen_active_power", 0.0f));
                _tagUpdates.OnNext(new TagData(ConnectionId, $"gMqt/GPA{i}/PPU_Gen_counter_active_power", 0.0f));
                _tagUpdates.OnNext(new TagData(ConnectionId, $"gMqt/GPA{i}/T404", 0.0f));
                _tagUpdates.OnNext(new TagData(ConnectionId, $"gMqt/GPA{i}/Hours", 0.0f));
                _tagUpdates.OnNext(new TagData(ConnectionId, $"gMqt/GPA{i}/Status_MWM", false));
                _tagUpdates.OnNext(new TagData(ConnectionId, $"gMqt/GPA{i}/HAS_IN_Word55_0", false));
            }
        }

        private async Task MockLoop(CancellationToken ct)
        {
            var lastTime = DateTime.UtcNow;
            int secondCounter = 0;
            
            // Local state for GPA mock
            float[] gpaActivePower = new float[7] { 0, 1200, 1200, 1200, 1200, 1200, 1200 };
            float[] gpaCounter = new float[7] { 0, 18000, 18000, 18000, 18000, 18000, 18000 };
            float[] gpaTemp = new float[7] { 0, 45, 45, 45, 45, 45, 45 };
            float[] gpaHours = new float[7] { 0, 90000, 90000, 90000, 90000, 90000, 90000 };

            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(100, ct);

                // Modbus Simulation Tick
                if (_modbusServerManager != null && _modbusServerManager.IsRunning)
                {
                    var now = DateTime.UtcNow;
                    double dt = (now - lastTime).TotalSeconds;
                    lastTime = now;
                    if (dt > 1.0) dt = 1.0;

                    try
                    {
                        _simulationEngine.ReadFromModbus(_modbusServerManager);
                        _simulationEngine.Tick(dt);
                        _simulationEngine.WriteToModbus(_modbusServerManager);
                        // In a real mock, we'd extract values from _simulationEngine and push to _tagUpdates here, 
                        // but skipping for brevity unless requested.
                    }
                    catch { }
                }
                else
                {
                    lastTime = DateTime.UtcNow;
                }

                // Complex Mock Simulation (once per second)
                secondCounter++;
                if (secondCounter >= 10)
                {
                    secondCounter = 0;

                    // ZAS Setpoint tracking
                    _zasActualPower += (_zasSetpointPower - _zasActualPower) * 0.1f + (float)(_random.NextDouble() - 0.5) * 4.0f;
                    if (_zasActualPower < 0) _zasActualPower = 0;
                    if (_zasActualPower > 1000) _zasActualPower = 1000;
                    _tagUpdates.OnNext(new TagData(ConnectionId, "gMqt/ZAS/ZAS_Actual_Power", _zasActualPower));

                    TickDemoPhysics(1.0);

                    // GPA 1 to 6 simulation
                    EmitDemoStates();

            for (int i = 1; i <= 6; i++)
                    {
                        gpaActivePower[i] += (float)(_random.NextDouble() - 0.5) * 20.0f;
                        if (gpaActivePower[i] < 0) gpaActivePower[i] = 0;
                        if (gpaActivePower[i] > 2000) gpaActivePower[i] = 2000;
                        _tagUpdates.OnNext(new TagData(ConnectionId, $"gMqt/GPA{i}/PPU_Gen_active_power", gpaActivePower[i]));

                        gpaCounter[i] += gpaActivePower[i] / 3600.0f;
                        _tagUpdates.OnNext(new TagData(ConnectionId, $"gMqt/GPA{i}/PPU_Gen_counter_active_power", gpaCounter[i]));

                        gpaTemp[i] += (float)(_random.NextDouble() - 0.5) * 0.5f;
                        if (gpaTemp[i] < 0) gpaTemp[i] = 0;
                        if (gpaTemp[i] > 150) gpaTemp[i] = 150;
                        _tagUpdates.OnNext(new TagData(ConnectionId, $"gMqt/GPA{i}/T404", gpaTemp[i]));

                        gpaHours[i] += 1.0f / 3600.0f;
                        _tagUpdates.OnNext(new TagData(ConnectionId, $"gMqt/GPA{i}/Hours", gpaHours[i]));

                        if (_random.Next(1, 100) == 50)
                        {
                            _tagUpdates.OnNext(new TagData(ConnectionId, $"gMqt/GPA{i}/HAS_IN_Word55_0", true)); // flip alarm
                        }
                    }
                }
            }
        }

        private void EmitDemoStates()
        {
            var now = DateTime.UtcNow;
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/T101/Level", _demoT101Level, TagQuality.Good, now));
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/T102/Level", _demoT102Level, TagQuality.Good, now));
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/R101/Level", _demoR101Level, TagQuality.Good, now));
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/R101/Temp", _demoR101Temp, TagQuality.Good, now));
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/R101/TempSp", _demoR101TempSp, TagQuality.Good, now));
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/R101/HeaterEnable", _demoR101HeaterEnable, TagQuality.Good, now));
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/R101/AgitatorCmd", _demoR101AgitatorCmd, TagQuality.Good, now));
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/R101/AgitatorFb", _demoR101AgitatorFb, TagQuality.Good, now));
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/R101/Mode", _demoR101ModeAuto, TagQuality.Good, now));

            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/P101/Cmd", _demoP101Cmd, TagQuality.Good, now));
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/P101/Fb", _demoP101Fb, TagQuality.Good, now));
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/P101/Mode", _demoP101ModeAuto, TagQuality.Good, now));

            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/YV101/Cmd", _demoYV101Cmd, TagQuality.Good, now));
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/YV101/OpenFb", _demoYV101OpenFb, TagQuality.Good, now));
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/YV101/CloseFb", _demoYV101CloseFb, TagQuality.Good, now));
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/YV101/Mode", _demoYV101ModeAuto, TagQuality.Good, now));

            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/P102/Cmd", _demoP102Cmd, TagQuality.Good, now));
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/P102/Fb", _demoP102Fb, TagQuality.Good, now));
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/P102/Mode", _demoP102ModeAuto, TagQuality.Good, now));
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/P102/FreqSp", _demoP102FreqSp, TagQuality.Good, now));
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/P102/FreqFb", _demoP102FreqFb, TagQuality.Good, now));

            double current = _demoP102Fb ? (1.4 + 0.21 * _demoP102FreqFb) : 0.0;
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/P102/Current", current, TagQuality.Good, now));

            uint sw = 0;
            if (_demoLinePower && !_demoFaultActive) sw |= 1u << 0; // Bit 0: Ready
            if (_demoP102Fb) sw |= 1u << 1;                         // Bit 1: Running
            if (_demoFaultActive) sw |= 1u << 2;                    // Bit 2: Overcurrent Fault
            if (_demoR101Temp > 110.0) sw |= 1u << 3;               // Bit 3: Overheat Warning
            if (!_demoP102ModeAuto) sw |= 1u << 4;                  // Bit 4: Local Panel [M]
            if (_demoP102FreqFb > 45.0) sw |= 1u << 5;              // Bit 5: High Frequency Warning
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/P102/StatusWord", sw, TagQuality.Good, now));

            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/FV102/Pos", _demoFV102Pos, TagQuality.Good, now));
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/YV103/Cmd", _demoYV103Cmd, TagQuality.Good, now));
            double heOut = _demoR101Temp - 0.15 * (_demoFV102Pos / 100.0) * (_demoR101Temp - 20.0);
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/HE101/TempOut", heOut, TagQuality.Good, now));
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/LinePower", _demoLinePower, TagQuality.Good, now));
            _tagUpdates.OnNext(new TagData(ConnectionId, "demo/FaultActive", _demoFaultActive, TagQuality.Good, now));
        }

        private void TickDemoPhysics(double dt)
        {
            _demoTransitTick++;

            // Line power interlock
            bool effectiveP101 = _demoLinePower && _demoP101Cmd;
            bool effectiveP102 = _demoLinePower && _demoP102Cmd && !_demoFaultActive;
            bool effectiveAg = _demoLinePower && _demoR101AgitatorCmd;

            // Delayed feedback simulation (1 tick transit visibility)
            _demoP101Fb = effectiveP101;
            _demoP102Fb = effectiveP102;
            _demoR101AgitatorFb = effectiveAg;
            _demoYV101OpenFb = _demoYV101Cmd;
            _demoYV101CloseFb = !_demoYV101Cmd;

            // VFD Frequency ramp (12 Hz/s)
            double targetHz = _demoP102Fb ? _demoP102FreqSp : 0.0;
            double deltaHz = targetHz - _demoP102FreqFb;
            double maxStep = 12.0 * dt;
            _demoP102FreqFb = Math.Abs(deltaHz) <= maxStep ? targetHz : _demoP102FreqFb + Math.Sign(deltaHz) * maxStep;

            if (_demoP102FreqFb > 48.5)
            {
                _demoFaultActive = true;
            }

            // Flow & Tank Levels
            if (_demoP101Fb && _demoYV101OpenFb && _demoT101Level > 20.0 && _demoR101Level < 480.0)
            {
                double flow = 12.0 * dt;
                _demoT101Level = Math.Max(20.0, _demoT101Level - flow);
                _demoR101Level = Math.Min(480.0, _demoR101Level + flow);
            }
            if (_demoT101Level < 150.0) _demoT101Level = 800.0; // Auto-refill feed tank for continuous demo

            if (_demoYV103Cmd && _demoR101Level > 20.0 && _demoT102Level < 480.0)
            {
                double drain = 15.0 * dt;
                _demoR101Level = Math.Max(20.0, _demoR101Level - drain);
                _demoT102Level = Math.Min(480.0, _demoT102Level + drain);
            }
            if (_demoT102Level > 460.0) _demoT102Level = 80.0; // Auto-empty product tank

            // Reactor Temperature
            double targetTemp = (_demoLinePower && _demoR101HeaterEnable) ? _demoR101TempSp : 22.0;
            double cooling = (_demoP102Fb ? (_demoP102FreqFb / 50.0) : 0.0) * (_demoFV102Pos / 100.0) * 6.0;
            _demoR101Temp += (targetTemp - _demoR101Temp) * 0.12 * dt - cooling * 0.1 * dt + (_random.NextDouble() - 0.5) * 0.4;
            _demoR101Temp = Math.Clamp(_demoR101Temp, 15.0, 155.0);

            EmitDemoStates();
        }
    }
}
