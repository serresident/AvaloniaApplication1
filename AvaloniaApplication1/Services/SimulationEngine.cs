using System;

namespace AvaloniaApplication1.Services
{
    public class SimulationEngine
    {
        private readonly Random _rand = new Random();

        // --- Transition States ---
        private double _waterValvePos;
        private double _dischargeValvePos;
        private double _coolingValvePos;
        private double _controlValvePos;
        private double _waterPumpSpeed;
        private double _coolingPumpSpeed;
        private double _feederASpeed;
        private double _feederBSpeed;
        private double _heaterPower;
        private double _heaterErrorSum;

        // --- Water Loading State ---
        public bool WaterPumpRun { get; set; }
        public bool WaterValveOpen { get; set; }
        public bool WaterPumpFeedback { get; set; }
        public bool WaterValveOpenedFeedback { get; set; }
        public double Totalizer { get; set; } // Liters
        public double TargetWaterVol { get; set; } // Liters
        public bool WaterBatchComplete { get; set; }
        public bool ResetTotalizer { get; set; }

        // --- Component A & B State ---
        public bool FeederARun { get; set; }
        public bool FeederAFeedback { get; set; }
        public double Weight1 { get; set; } // Component A kg
        public double TargetWeightA { get; set; } // kg
        public bool BatchAComplete { get; set; }

        public bool FeederBRun { get; set; }
        public bool FeederBFeedback { get; set; }
        public double Weight2 { get; set; } // Component B kg
        public double TargetWeightB { get; set; } // kg
        public bool BatchBComplete { get; set; }

        public double Weight3 { get; set; } // Total Vessel Weight (A + B + Water)
        public double Weight4 { get; set; } // Tare (configured in UI or Holding Registers)
        public double Weight5 { get; set; } // Waste container

        // --- Pressure Loop State ---
        public double Press1 { get; set; } = 1.0; // Process Pressure (bar)
        public double Press2 { get; set; } = 1.0; // Inlet Pressure (bar)
        public double Press3 { get; set; } = 1.0; // Outlet Pressure (bar)
        public double Press4 { get; set; } = 0.0; // Hydraulic Line Pressure (bar)
        public double Press5 { get; set; } = 0.05; // Vacuum Level (bar)
        public double PumpSpeedCmd { get; set; } // Water Feed Pump Speed Command (%)
        public double ControlValvePos { get; set; } // Control Valve Position (%)
        public bool PressureReliefOpen { get; set; }
        public bool PressureReliefFeedback { get; set; }
        public bool AlarmHiPress { get; set; }

        // --- Flow Loop State ---
        public double Flow1 { get; set; } // Process Flow Rate (L/min)
        public double Flow2 { get; set; } // Component A Mass Flow (kg/min)
        public double Flow3 { get; set; } // Component B Mass Flow (kg/min)
        public double Flow4 { get; set; } // Circulation Flow (L/min)
        public double Flow5 { get; set; } // Cooling Water Flow (L/min)

        // --- Temperature Loop State ---
        public double Temp1 { get; set; } = 20.0; // Process Temperature (°C)
        public double Temp2 { get; set; } = 22.0; // Ambient Temperature (°C)
        public double Temp3 { get; set; } = 22.0; // Pump Motor Temperature (°C)
        public double Temp4 { get; set; } = 15.0; // Cooling Water Temperature (°C)
        public double Temp5 { get; set; } = 22.0; // Exhaust Air Temperature (°C)
        public bool HeaterEnable { get; set; }
        public bool HeaterAuto { get; set; }
        public bool HeaterFeedback { get; set; }
        public double HeaterPowerCmd { get; set; } // Heating Element Power (%)
        public bool CoolingPumpRun { get; set; }
        public bool CoolingPumpFeedback { get; set; }
        public double CoolingValvePos { get; set; } // Cooling Water Valve Position (%)
        public bool AlarmHiTemp { get; set; }

        // --- Fan Control State ---
        public bool FanControlAuto { get; set; }
        public bool FanRun { get; set; }
        public bool FanFeedback { get; set; }

        // --- Loop Setpoints (for UI & Reference) ---
        public double PressureSP { get; set; }
        public double FlowSP { get; set; }
        public double TempSP { get; set; }

        public void ResetSimulation()
        {
            Totalizer = 0.0;
            Weight1 = 0.0;
            Weight2 = 0.0;
            Weight3 = 0.0;
            Flow1 = 0.0;
            Flow2 = 0.0;
            Flow3 = 0.0;
            Flow4 = 0.0;
            Flow5 = 0.0;
            Temp1 = Temp2;
            Temp3 = Temp2;
            Temp4 = 15.0;
            Temp5 = Temp2;
            Press1 = 1.0;
            Press2 = 1.0;
            Press3 = 1.0;
            Press4 = 0.0;
            WaterBatchComplete = false;
            BatchAComplete = false;
            BatchBComplete = false;
            WaterPumpRun = false;
            WaterValveOpen = false;
            FeederARun = false;
            FeederBRun = false;
            HeaterEnable = false;
            HeaterAuto = false;
            CoolingPumpRun = false;
            PressureReliefOpen = false;

            // Reset transition states
            _waterValvePos = 0.0;
            _dischargeValvePos = 0.0;
            _coolingValvePos = 0.0;
            _controlValvePos = 0.0;
            _waterPumpSpeed = 0.0;
            _coolingPumpSpeed = 0.0;
            _feederASpeed = 0.0;
            _feederBSpeed = 0.0;
            _heaterPower = 0.0;
            _heaterErrorSum = 0.0;
        }

        public void ReadFromModbus(ModbusServerManager server)
        {
            if (!server.IsRunning) return;

            // --- Coils (0x) ---
            WaterPumpRun = server.ReadCoil(0);
            WaterValveOpen = server.ReadCoil(1);
            FeederARun = server.ReadCoil(2);
            FeederBRun = server.ReadCoil(3);
            PressureReliefOpen = server.ReadCoil(4);
            HeaterEnable = server.ReadCoil(5);
            CoolingPumpRun = server.ReadCoil(6);
            IsRunningBit7Sync(server); // Helper for sync coil 7/8/9/10

            // --- Holding Registers (4x) ---
            ushort reg0 = server.ReadHoldingRegister(0);
            ushort reg1 = server.ReadHoldingRegister(1);
            ushort reg2 = server.ReadHoldingRegister(2);
            ushort reg3 = server.ReadHoldingRegister(3);
            TargetWaterVol = server.ReadHoldingRegister(4);         // Liters
            TargetWeightA = server.ReadHoldingRegister(5) / 10.0;   // 0.1 kg
            TargetWeightB = server.ReadHoldingRegister(6) / 10.0;   // 0.1 kg
            ushort reg7 = server.ReadHoldingRegister(7);
            ushort reg8 = server.ReadHoldingRegister(8);
            ushort reg9 = server.ReadHoldingRegister(9);

            // Read float registers
            float floatVal0 = server.ReadHoldingFloat(20);
            float floatVal1 = server.ReadHoldingFloat(22);
            float floatVal2 = server.ReadHoldingFloat(24);
            float floatVal3 = server.ReadHoldingFloat(26);
            float floatVal7 = server.ReadHoldingFloat(28);
            float floatVal8 = server.ReadHoldingFloat(30);
            float floatVal9 = server.ReadHoldingFloat(32);

            // Sync: ControlValvePos
            double valFrom16Bit0 = reg0 / 10.0;
            if (Math.Abs(floatVal0 - ControlValvePos) > 0.001 && floatVal0 >= 0 && floatVal0 <= 100)
            {
                ControlValvePos = floatVal0;
            }
            else if (Math.Abs(valFrom16Bit0 - ControlValvePos) > 0.001)
            {
                ControlValvePos = valFrom16Bit0;
            }

            // Sync: PumpSpeedCmd
            double valFrom16Bit1 = reg1 / 10.0;
            if (Math.Abs(floatVal1 - PumpSpeedCmd) > 0.001 && floatVal1 >= 0 && floatVal1 <= 100)
            {
                PumpSpeedCmd = floatVal1;
            }
            else if (Math.Abs(valFrom16Bit1 - PumpSpeedCmd) > 0.001)
            {
                PumpSpeedCmd = valFrom16Bit1;
            }

            // Sync: HeaterPowerCmd
            double valFrom16Bit2 = reg2 / 10.0;
            if (Math.Abs(floatVal2 - HeaterPowerCmd) > 0.001 && floatVal2 >= 0 && floatVal2 <= 100)
            {
                HeaterPowerCmd = floatVal2;
            }
            else if (Math.Abs(valFrom16Bit2 - HeaterPowerCmd) > 0.001)
            {
                HeaterPowerCmd = valFrom16Bit2;
            }

            // Sync: CoolingValvePos
            double valFrom16Bit3 = reg3 / 10.0;
            if (Math.Abs(floatVal3 - CoolingValvePos) > 0.001 && floatVal3 >= 0 && floatVal3 <= 100)
            {
                CoolingValvePos = floatVal3;
            }
            else if (Math.Abs(valFrom16Bit3 - CoolingValvePos) > 0.001)
            {
                CoolingValvePos = valFrom16Bit3;
            }

            // Sync: PressureSP
            double valFrom16Bit7 = reg7 / 100.0;
            if (Math.Abs(floatVal7 - PressureSP) > 0.001)
            {
                PressureSP = floatVal7;
            }
            else if (Math.Abs(valFrom16Bit7 - PressureSP) > 0.001)
            {
                PressureSP = valFrom16Bit7;
            }

            // Sync: FlowSP
            double valFrom16Bit8 = reg8 / 10.0;
            if (Math.Abs(floatVal8 - FlowSP) > 0.001)
            {
                FlowSP = floatVal8;
            }
            else if (Math.Abs(valFrom16Bit8 - FlowSP) > 0.001)
            {
                FlowSP = valFrom16Bit8;
            }

            // Sync: TempSP
            double valFrom16Bit9 = reg9 / 10.0;
            if (Math.Abs(floatVal9 - TempSP) > 0.001)
            {
                TempSP = floatVal9;
            }
            else if (Math.Abs(valFrom16Bit9 - TempSP) > 0.001)
            {
                TempSP = valFrom16Bit9;
            }
        }

        private void IsRunningBit7Sync(ModbusServerManager server)
        {
            FanControlAuto = server.ReadCoil(7);
            FanRun = server.ReadCoil(8);
            ResetTotalizer = server.ReadCoil(9);
            HeaterAuto = server.ReadCoil(10);
        }

        public void WriteToModbus(ModbusServerManager server)
        {
            if (!server.IsRunning) return;

            // --- Discrete Inputs (1x) ---
            server.WriteDiscreteInput(0, WaterPumpFeedback);
            server.WriteDiscreteInput(1, WaterValveOpenedFeedback);
            server.WriteDiscreteInput(2, FeederAFeedback);
            server.WriteDiscreteInput(3, FeederBFeedback);
            server.WriteDiscreteInput(4, PressureReliefFeedback);
            server.WriteDiscreteInput(5, HeaterFeedback);
            server.WriteDiscreteInput(6, CoolingPumpFeedback);
            server.WriteDiscreteInput(7, FanFeedback);
            server.WriteDiscreteInput(8, AlarmHiTemp);
            server.WriteDiscreteInput(9, AlarmHiPress);
            server.WriteDiscreteInput(10, WaterBatchComplete);
            server.WriteDiscreteInput(11, BatchAComplete);
            server.WriteDiscreteInput(12, BatchBComplete);

            // --- Input Registers (3x) ---
            server.WriteInputRegister(0, (ushort)Math.Max(0, Math.Round(Temp1 * 10)));
            server.WriteInputRegister(1, (ushort)Math.Max(0, Math.Round(Temp2 * 10)));
            server.WriteInputRegister(2, (ushort)Math.Max(0, Math.Round(Temp3 * 10)));
            server.WriteInputRegister(3, (ushort)Math.Max(0, Math.Round(Temp4 * 10)));
            server.WriteInputRegister(4, (ushort)Math.Max(0, Math.Round(Temp5 * 10)));

            server.WriteInputRegister(5, (ushort)Math.Max(0, Math.Round(Press1 * 100)));
            server.WriteInputRegister(6, (ushort)Math.Max(0, Math.Round(Press2 * 100)));
            server.WriteInputRegister(7, (ushort)Math.Max(0, Math.Round(Press3 * 100)));
            server.WriteInputRegister(8, (ushort)Math.Max(0, Math.Round(Press4 * 100)));
            server.WriteInputRegister(9, (ushort)Math.Max(0, Math.Round(Press5 * 100)));

            server.WriteInputRegister(10, (ushort)Math.Max(0, Math.Round(Flow1 * 10)));
            server.WriteInputRegister(11, (ushort)Math.Max(0, Math.Round(Flow2 * 10)));
            server.WriteInputRegister(12, (ushort)Math.Max(0, Math.Round(Flow3 * 10)));
            server.WriteInputRegister(13, (ushort)Math.Max(0, Math.Round(Flow4 * 10)));
            server.WriteInputRegister(14, (ushort)Math.Max(0, Math.Round(Flow5 * 10)));

            server.WriteInputRegister(15, (ushort)Math.Max(0, Math.Round(Weight1 * 10)));
            server.WriteInputRegister(16, (ushort)Math.Max(0, Math.Round(Weight2 * 10)));
            server.WriteInputRegister(17, (ushort)Math.Max(0, Math.Round(Weight3 * 10)));
            server.WriteInputRegister(18, (ushort)Math.Max(0, Math.Round(Weight4 * 10)));
            server.WriteInputRegister(19, (ushort)Math.Max(0, Math.Round(Weight5 * 10)));

            // Water Totalizer (32-bit Integer, registers 20 and 21)
            int totalizerInt = (int)Math.Round(Totalizer * 10); // scale 0.1L per unit
            server.WriteInputInt32(20, totalizerInt);

            // Float process values (32-bit Float, registers 22-35)
            server.WriteInputFloat(22, (float)Temp1);
            server.WriteInputFloat(24, (float)Press1);
            server.WriteInputFloat(26, (float)Flow4); // L/min (Discharge flow rate)
            server.WriteInputFloat(28, (float)Weight3); // Level / Total weight (Tank level)
            server.WriteInputFloat(30, (float)Weight1);
            server.WriteInputFloat(32, (float)Weight2);
            server.WriteInputFloat(34, (float)Totalizer);

            // --- Sync modified commands back to Coils ---
            server.WriteCoil(0, WaterPumpRun);
            server.WriteCoil(1, WaterValveOpen);
            server.WriteCoil(2, FeederARun);
            server.WriteCoil(3, FeederBRun);
            server.WriteCoil(5, HeaterEnable);
            server.WriteCoil(9, ResetTotalizer);
            server.WriteCoil(10, HeaterAuto);

            // --- Sync control commands back to Holding Registers ---
            server.WriteHoldingRegister(0, (ushort)Math.Clamp(Math.Round(_controlValvePos * 10.0), 0.0, 1000.0));
            server.WriteHoldingRegister(1, (ushort)Math.Clamp(Math.Round(PumpSpeedCmd * 10.0), 0.0, 1000.0));
            server.WriteHoldingRegister(2, (ushort)Math.Clamp(Math.Round(HeaterPowerCmd * 10.0), 0.0, 1000.0));
            server.WriteHoldingRegister(3, (ushort)Math.Clamp(Math.Round(_coolingValvePos * 10.0), 0.0, 1000.0));

            // Write 32-bit floats
            server.WriteHoldingFloat(20, (float)_controlValvePos);
            server.WriteHoldingFloat(22, (float)PumpSpeedCmd);
            server.WriteHoldingFloat(24, (float)HeaterPowerCmd);
            server.WriteHoldingFloat(26, (float)_coolingValvePos);
            server.WriteHoldingFloat(28, (float)PressureSP);
            server.WriteHoldingFloat(30, (float)FlowSP);
            server.WriteHoldingFloat(32, (float)TempSP);
        }

        public void Tick(double dt)
        {
            // Reset Totalizer Command (resets all dosed ingredients)
            if (ResetTotalizer)
            {
                Totalizer = 0.0;
                Weight1 = 0.0;
                Weight2 = 0.0;
                Weight3 = 0.0;
                WaterBatchComplete = false;
                BatchAComplete = false;
                BatchBComplete = false;
                ResetTotalizer = false;
            }

            // --- Physical State Transitions ---
            // Water pump ramp
            double targetWaterPumpSpeed = WaterPumpRun ? PumpSpeedCmd : 0.0;
            _waterPumpSpeed += (targetWaterPumpSpeed - _waterPumpSpeed) * dt / 0.5;
            _waterPumpSpeed = Math.Clamp(_waterPumpSpeed, 0.0, 100.0);
            WaterPumpFeedback = _waterPumpSpeed > 10.0;

            // Water valve opening ramp
            double targetWaterValvePos = WaterValveOpen ? 100.0 : 0.0;
            _waterValvePos += (targetWaterValvePos - _waterValvePos) * dt / 2.0;
            _waterValvePos = Math.Clamp(_waterValvePos, 0.0, 100.0);
            WaterValveOpenedFeedback = _waterValvePos >= 95.0;

            // Feeder A ramp
            double targetFeederASpeed = FeederARun ? 100.0 : 0.0;
            _feederASpeed += (targetFeederASpeed - _feederASpeed) * dt / 0.5;
            _feederASpeed = Math.Clamp(_feederASpeed, 0.0, 100.0);
            FeederAFeedback = _feederASpeed > 50.0;

            // Feeder B ramp
            double targetFeederBSpeed = FeederBRun ? 100.0 : 0.0;
            _feederBSpeed += (targetFeederBSpeed - _feederBSpeed) * dt / 0.5;
            _feederBSpeed = Math.Clamp(_feederBSpeed, 0.0, 100.0);
            FeederBFeedback = _feederBSpeed > 50.0;

            // Discharge / Pressure Relief valve ramp (Outlet Valve)
            double targetDischargeValvePos = PressureReliefOpen ? 100.0 : 0.0;
            _dischargeValvePos += (targetDischargeValvePos - _dischargeValvePos) * dt / 2.0;
            _dischargeValvePos = Math.Clamp(_dischargeValvePos, 0.0, 100.0);
            PressureReliefFeedback = _dischargeValvePos >= 95.0;

            // Cooling Pump ramp
            double targetCoolingPumpSpeed = CoolingPumpRun ? 100.0 : 0.0;
            _coolingPumpSpeed += (targetCoolingPumpSpeed - _coolingPumpSpeed) * dt / 0.5;
            _coolingPumpSpeed = Math.Clamp(_coolingPumpSpeed, 0.0, 100.0);
            CoolingPumpFeedback = _coolingPumpSpeed > 50.0;

            // Cooling Valve position ramp
            _coolingValvePos += (CoolingValvePos - _coolingValvePos) * dt / 2.0;
            _coolingValvePos = Math.Clamp(_coolingValvePos, 0.0, 100.0);

            // Control Valve position ramp
            _controlValvePos += (ControlValvePos - _controlValvePos) * dt / 2.0;
            _controlValvePos = Math.Clamp(_controlValvePos, 0.0, 100.0);

            // Heater actual power ramp
            double targetHeaterPower = HeaterEnable ? HeaterPowerCmd : 0.0;
            _heaterPower += (targetHeaterPower - _heaterPower) * dt / 1.0;
            _heaterPower = Math.Clamp(_heaterPower, 0.0, 100.0);
            HeaterFeedback = HeaterEnable && _heaterPower > 5.0;

            // --- Flow Calculations ---
            // 1. Water inlet flow
            double targetFlow1 = 0.0;
            if (WaterPumpFeedback && WaterValveOpenedFeedback)
            {
                if (FlowSP > 0)
                {
                    // Automatic regulation of control valve pos to match flow setpoint
                    if (PumpSpeedCmd < 10.0) PumpSpeedCmd = 80.0;
                    double error = FlowSP - Flow1;
                    ControlValvePos = Math.Clamp(ControlValvePos + error * dt * 8.0, 0.0, 100.0);
                }
                targetFlow1 = (_waterPumpSpeed / 100.0) * (_waterValvePos / 100.0) * 150.0; // Max 150 L/min
            }
            Flow1 += (targetFlow1 - Flow1) * dt / 0.8;
            if (Flow1 < 0.01) Flow1 = 0.0;

            // 2. Feeder A inlet flow
            double targetFlow2 = FeederAFeedback ? 25.0 : 0.0; // 25 kg/min
            Flow2 += (targetFlow2 - Flow2) * dt / 0.5;
            if (Flow2 < 0.01) Flow2 = 0.0;

            // 3. Feeder B inlet flow
            double targetFlow3 = FeederBFeedback ? 18.0 : 0.0; // 18 kg/min
            Flow3 += (targetFlow3 - Flow3) * dt / 0.5;
            if (Flow3 < 0.01) Flow3 = 0.0;

            // 4. Outlet Discharge flow (gravity-driven, depends on reactor weight/level)
            double targetFlow4 = 0.0;
            if (PressureReliefFeedback)
            {
                targetFlow4 = 120.0 * Math.Sqrt(Weight3 / 1000.0); // max 120 L/min when full
            }
            Flow4 += (targetFlow4 - Flow4) * dt / 1.0;
            if (Flow4 < 0.01) Flow4 = 0.0;

            // 5. Cooling flow
            double targetFlow5 = CoolingPumpFeedback ? (_coolingValvePos / 100.0) * 30.0 : 0.0; // Max 30 L/min
            Flow5 += (targetFlow5 - Flow5) * dt / 0.8;
            if (Flow5 < 0.01) Flow5 = 0.0;

            // --- Mass Accumulations and Emptying ---
            double dW = Flow1 * (dt / 60.0);
            double dA = Flow2 * (dt / 60.0);
            double dB = Flow3 * (dt / 60.0);
            double dOut = Flow4 * (dt / 60.0);

            // Adding ingredients
            Weight1 += dA;
            Weight2 += dB;
            Totalizer += dW;

            // Total weight before discharge
            Weight3 = Weight1 + Weight2 + Totalizer;

            // Dosing batch completion checks
            if (TargetWaterVol > 0 && Totalizer >= TargetWaterVol)
            {
                WaterBatchComplete = true;
                WaterPumpRun = false;
                WaterValveOpen = false;
            }
            if (TargetWeightA > 0 && Weight1 >= TargetWeightA)
            {
                BatchAComplete = true;
                FeederARun = false;
            }
            if (TargetWeightB > 0 && Weight2 >= TargetWeightB)
            {
                BatchBComplete = true;
                FeederBRun = false;
            }

            // Discharging ingredients proportionally
            if (dOut > 0.0 && Weight3 > 0.0)
            {
                double f_A = Weight1 / Weight3;
                double f_B = Weight2 / Weight3;
                double f_W = Totalizer / Weight3;

                Weight1 = Math.Max(0.0, Weight1 - dOut * f_A);
                Weight2 = Math.Max(0.0, Weight2 - dOut * f_B);
                Totalizer = Math.Max(0.0, Totalizer - dOut * f_W);
                Weight3 = Weight1 + Weight2 + Totalizer;
            }

            if (Weight3 < 0.01)
            {
                Weight1 = 0.0;
                Weight2 = 0.0;
                Totalizer = 0.0;
                Weight3 = 0.0;
            }

            // --- Thermodynamic Mixing and Thermal Calculations ---
            // 1. Mixing temperature change
            double totalIncoming = dW + dA + dB;
            if (totalIncoming > 0.0)
            {
                // Water enters at 15°C, Feeders A and B enter at ambient Temp2 (22°C)
                double incomingHeat = (15.0 * dW) + (Temp2 * dA) + (Temp2 * dB);
                if (Weight3 > totalIncoming)
                {
                    double prevWeight = Weight3 - totalIncoming;
                    Temp1 = (Temp1 * prevWeight + incomingHeat) / Weight3;
                }
                else
                {
                    Temp1 = incomingHeat / totalIncoming;
                }
            }

            // 2. Heater PI control loop & actual heating rate
            if (HeaterAuto)
            {
                if (TempSP > 0)
                {
                    double error = TempSP - Temp1;
                    _heaterErrorSum = Math.Clamp(_heaterErrorSum + error * dt, -100.0, 100.0);
                    HeaterPowerCmd = Math.Clamp(error * 10.0 + _heaterErrorSum * 0.5, 0.0, 100.0);
                }
                else
                {
                    HeaterPowerCmd = 0.0;
                    _heaterErrorSum = 0.0;
                }
            }
            else
            {
                _heaterErrorSum = 0.0;
            }

            // Heating rate
            double effectiveMass = Math.Max(50.0, Weight3); // clamp to avoid infinite heating rate
            double heatingRate = (_heaterPower / 100.0) * 150.0 / effectiveMass; // °C/sec

            // Cooling rate
            double coolingRate = Flow5 * 0.15 * (Temp1 - Temp4) / effectiveMass; // °C/sec

            // Passive heat loss to ambient
            double passiveRate = (Temp1 - Temp2) * 0.01;

            // Temperature update
            Temp1 += (heatingRate - coolingRate - passiveRate) * dt;
            Temp1 = Math.Clamp(Temp1, Math.Min(Temp2, Temp4), 100.0);

            AlarmHiTemp = Temp1 > 85.0;

            // --- Auxiliary Temperatures / Pressures ---
            // Pump motor heats up with speed
            double targetTemp3 = Temp2 + (WaterPumpRun ? (_waterPumpSpeed / 100.0) * 35.0 : 0.0);
            Temp3 += (targetTemp3 - Temp3) * dt / 12.0;

            // Cooling water temp rises slightly under thermal load
            double targetTemp4 = 15.0 + (Flow5 > 0 ? 3.0 : 0.0);
            Temp4 += (targetTemp4 - Temp4) * dt / 8.0;

            // Exhaust air temperature
            double targetTemp5 = Temp2 + (FanFeedback ? 1.0 : 6.0);
            Temp5 += (targetTemp5 - Temp5) * dt / 10.0;

            // --- Pressure Control Loop Physics ---
            // Sealed compression: pressure rises as water is pumped in, gas volume shrinks
            // Volume of reactor is 1000L. Gas volume is 1000 - Weight3 (min 50L)
            double gasVolume = Math.Max(50.0, 1000.0 - Weight3);
            double compressionRatio = 1000.0 / gasVolume;
            // Gay-Lussac compression pressure + thermal expansion
            double basePress = 1.0 * compressionRatio * (Temp1 + 273.15) / (20.0 + 273.15);

            // Water pump head pressure (max 4.0 bar added)
            double pumpPressureEffect = WaterPumpRun ? (_waterPumpSpeed / 100.0) * 4.0 : 0.0;

            // Pressure SP automatic regulation of pump speed/power
            if (WaterPumpRun && PressureSP > 1.0)
            {
                double error = PressureSP - Press1;
                PumpSpeedCmd = Math.Clamp(PumpSpeedCmd + error * dt * 5.0, 0.0, 100.0);
            }

            double targetPress = basePress + pumpPressureEffect;

            // Vent opening (relief valve or pressure control valve)
            double ventOpening = Math.Max(_dischargeValvePos, _controlValvePos);
            if (ventOpening > 0.0)
            {
                // Drops rapidly towards 1.0 bar (ambient dump)
                double ventRate = (ventOpening / 100.0) * 1.5;
                Press1 += (1.0 - Press1) * ventRate * dt;
            }
            else
            {
                Press1 += (targetPress - Press1) * dt / 1.5;
            }

            // Add process noise to pressure signal
            if (Press1 > 1.01)
            {
                Press1 += (_rand.NextDouble() - 0.5) * 0.02;
            }
            if (Press1 < 1.0) Press1 = 1.0;

            AlarmHiPress = Press1 > 6.0;

            // Auxiliary pressures
            Press2 = WaterPumpRun ? 3.0 + (_rand.NextDouble() - 0.5) * 0.1 : 1.0;
            Press3 = Press1 * 0.96;
            Press4 = WaterPumpRun ? 12.0 + (_waterPumpSpeed / 100.0) * 6.0 : 0.0;
            Press5 = 0.05;

            // Fan control
            if (FanControlAuto)
            {
                FanFeedback = Temp1 > 45.0; // Auto runs above 45 °C
                FanRun = FanFeedback;
            }
            else
            {
                FanFeedback = FanRun;
            }
        }
    }
}
