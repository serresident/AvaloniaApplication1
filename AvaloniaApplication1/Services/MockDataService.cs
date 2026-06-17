using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaApplication1.Services
{
    public class MockDataService : IMockDataService
    {
        private readonly System.Reactive.Subjects.Subject<Models.TagData> _tagUpdates = new();
        public IObservable<Models.TagData> TagUpdates => _tagUpdates;

        private readonly ConcurrentDictionary<string, object> _currentValues = new();
        private CancellationTokenSource? _cts;
        private Random _random = new();
        private double _temperature = 20.0;

        public void StartSimulation()
        {
            _cts = new CancellationTokenSource();
            
            // Initialize some default values
            _currentValues["plc1_40001"] = 20.0f; // Temperature
            _currentValues["plc1_00001"] = false; // Pump Status
            _currentValues["mqtt1_Pigment/ZAS/ZAS_Actual_Power"] = 150.0f; // ZAS Actual Power
            _currentValues["mqtt1_Pigment/ZAS/ZAS_Setpoint_Power"] = 200.0f; // ZAS Setpoint Power

            // Seed HMI Valve tags
            _currentValues["mqtt1_gMqt/Mixer/Valve_YV_W1"] = true; // Cutoff open command
            _currentValues["mqtt1_gMqt/Mixer/Valve_YV_W1_FB"] = false; // Cutoff feedback closed (mismatched)
            _currentValues["mqtt1_gMqt/Mixer/Valve_YV_S1"] = 75.0f; // Regulating setpoint command
            _currentValues["mqtt1_gMqt/Mixer/Valve_YV_S1_FB"] = 30.0f; // Regulating feedback (mismatched)
            _currentValues["mqtt1_gMqt/Mixer/Valve_YV_S1_Mode"] = false; // Regulating mode: Manual (false)
            _currentValues["mqtt1_gMqt/Mixer/Valve_YV_M1"] = false;
            _currentValues["mqtt1_gMqt/Mixer/Valve_YV_M1_FB"] = false;

            for (int i = 1; i <= 6; i++)
            {
                _currentValues[$"mqtt1_Pigment/GPA{i}/PPU_Gen_active_power"] = 1100.0f + i * 50.0f;
                _currentValues[$"mqtt1_Pigment/GPA{i}/PPU_Gen_counter_active_power"] = 18000.0f + i * 150.5f;
                _currentValues[$"mqtt1_Pigment/GPA{i}/T404"] = 42.0f + i * 0.8f;
                _currentValues[$"mqtt1_Pigment/GPA{i}/Hours"] = 90000.0f + i * 100.0f;
                _currentValues[$"mqtt1_Pigment/GPA{i}/Status_MWM"] = true;
                _currentValues[$"mqtt1_Pigment/GPA{i}/HAS_IN_Word55_0"] = (i == 3 || i == 5) ? true : false;
            }
            
            Task.Run(() => SimulationLoop(_cts.Token), _cts.Token);
        }

        public void StopSimulation()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
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
            _tagUpdates.OnNext(new Models.TagData(connId, address, value));
        }

        private async Task SimulationLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(1000, ct);

                // Simulate Temperature fluctuating
                _temperature += (_random.NextDouble() - 0.5) * 2.0; // Random walk +/- 1.0
                if (_temperature < 0) _temperature = 0;
                if (_temperature > 100) _temperature = 100;
                
                UpdateValue("plc1", "40001", (float)_temperature);

                // Simulate ZAS Setpoint following: ZAS power moves towards setpoint
                var setpoint = GetCurrentValue("mqtt1", "Pigment/ZAS/ZAS_Setpoint_Power") is float s ? s : 200.0f;
                var currentPower = GetCurrentValue("mqtt1", "Pigment/ZAS/ZAS_Actual_Power") is float p ? p : 150.0f;
                
                currentPower += (setpoint - currentPower) * 0.1f + (float)(_random.NextDouble() - 0.5) * 4.0f;
                if (currentPower < 0) currentPower = 0;
                if (currentPower > 1000) currentPower = 1000;
                UpdateValue("mqtt1", "Pigment/ZAS/ZAS_Actual_Power", currentPower);

                // Simulate GPA 1 to 6
                for (int i = 1; i <= 6; i++)
                {
                    // Active power fluctuation
                    var activePower = GetCurrentValue("mqtt1", $"Pigment/GPA{i}/PPU_Gen_active_power") is float ap ? ap : 1200.0f;
                    activePower += (float)(_random.NextDouble() - 0.5) * 20.0f;
                    if (activePower < 0) activePower = 0;
                    if (activePower > 2000) activePower = 2000;
                    UpdateValue("mqtt1", $"Pigment/GPA{i}/PPU_Gen_active_power", activePower);

                    // Counter increments based on power
                    var counter = GetCurrentValue("mqtt1", $"Pigment/GPA{i}/PPU_Gen_counter_active_power") is float cVal ? cVal : 18000.0f;
                    counter += activePower / 3600.0f;
                    UpdateValue("mqtt1", $"Pigment/GPA{i}/PPU_Gen_counter_active_power", counter);

                    // Temp fluctuation
                    var temp = GetCurrentValue("mqtt1", $"Pigment/GPA{i}/T404") is float tVal ? tVal : 45.0f;
                    temp += (float)(_random.NextDouble() - 0.5) * 0.5f;
                    if (temp < 0) temp = 0;
                    if (temp > 150) temp = 150;
                    UpdateValue("mqtt1", $"Pigment/GPA{i}/T404", temp);

                    // Operating hours increment slowly
                    var hours = GetCurrentValue("mqtt1", $"Pigment/GPA{i}/Hours") is float hVal ? hVal : 90000.0f;
                    hours += 1.0f / 3600.0f; // 1s
                    UpdateValue("mqtt1", $"Pigment/GPA{i}/Hours", hours);
                    
                    // Toggle alarms occasionally
                    if (_random.Next(1, 100) == 50)
                    {
                        var alarm = GetCurrentValue("mqtt1", $"Pigment/GPA{i}/HAS_IN_Word55_0") is bool aBool ? aBool : false;
                        UpdateValue("mqtt1", $"Pigment/GPA{i}/HAS_IN_Word55_0", !alarm);
                    }
                }

                // Simulate YV_S1 Regulating Valve
                bool isAuto = GetCurrentValue("mqtt1", "gMqt/Mixer/Valve_YV_S1_Mode") is bool modeVal && modeVal;
                float sSetpoint = GetCurrentValue("mqtt1", "gMqt/Mixer/Valve_YV_S1") is float spF ? spF : 75.0f;
                if (isAuto)
                {
                    // In Auto mode, simulate PLC program modulating the setpoint
                    sSetpoint += (float)(_random.NextDouble() - 0.5) * 6.0f;
                    sSetpoint = Math.Clamp(sSetpoint, 30.0f, 90.0f);
                    UpdateValue("mqtt1", "gMqt/Mixer/Valve_YV_S1", sSetpoint);
                }

                float sFeedback = GetCurrentValue("mqtt1", "gMqt/Mixer/Valve_YV_S1_FB") is float fbF ? fbF : 30.0f;
                // Feedback slowly approaches setpoint (simulating actuator travel speed)
                if (Math.Abs(sSetpoint - sFeedback) > 0.1f)
                {
                    sFeedback += (sSetpoint - sFeedback) * 0.18f;
                    UpdateValue("mqtt1", "gMqt/Mixer/Valve_YV_S1_FB", sFeedback);
                }

                // Simulate YV_W1 Cutoff Valve (aligns after a brief delay)
                bool wCmd = GetCurrentValue("mqtt1", "gMqt/Mixer/Valve_YV_W1") is bool wCmdB && wCmdB;
                bool wFb = GetCurrentValue("mqtt1", "gMqt/Mixer/Valve_YV_W1_FB") is bool wFbB && wFbB;
                if (wCmd != wFb)
                {
                    // 30% chance to align on each tick (approx 3 seconds travel time)
                    if (_random.Next(0, 10) < 3)
                    {
                        UpdateValue("mqtt1", "gMqt/Mixer/Valve_YV_W1_FB", wCmd);
                    }
                }

                // Simulate YV_M1 Cutoff Valve (instant alignment)
                bool mCmd = GetCurrentValue("mqtt1", "gMqt/Mixer/Valve_YV_M1") is bool mCmdB && mCmdB;
                UpdateValue("mqtt1", "gMqt/Mixer/Valve_YV_M1_FB", mCmd);
            }
        }

        private void UpdateValue(string connId, string address, object value)
        {
            var key = $"{connId}_{address}";
            _currentValues[key] = value;
            _tagUpdates.OnNext(new Models.TagData(connId, address, value));
        }
    }
}