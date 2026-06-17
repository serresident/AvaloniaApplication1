using System;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaApplication1.Models;

namespace AvaloniaApplication1.Services.Protocols
{
    public class MockProtocolDriver : IProtocolDriver
    {
        public string ConnectionId { get; }
        private readonly Subject<TagData> _tagUpdates = new();
        private readonly Random _random = new();
        private readonly SimulationEngine _simulationEngine = new();
        private ModbusServerManager? _modbusServerManager;

        public IObservable<TagData> TagUpdates => _tagUpdates;

        // Maintain some internal state for the mock
        private float _zasActualPower = 150.0f;
        private float _zasSetpointPower = 200.0f;
        
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
            _ = Task.Run(() => MockLoop(cancellationToken), cancellationToken);

            return Task.CompletedTask;
        }

        public Task WriteAsync(string address, object value, CancellationToken cancellationToken)
        {
            // In mock mode, writing simply echoes the value back as the new state
            if (address == "gMqt/ZAS/ZAS_Setpoint_Power" && float.TryParse(value?.ToString(), out float fVal))
            {
                _zasSetpointPower = fVal;
            }

            _tagUpdates.OnNext(new TagData(ConnectionId, address, value));
            return Task.CompletedTask;
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

                    // GPA 1 to 6 simulation
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
    }
}
