import socket
import struct
import time
import subprocess
import os

def send_modbus_raw(s, fc, unit_id, data):
    header = struct.pack('>HHHB', 1, 0, len(data) + 2, unit_id)
    packet = header + struct.pack('>B', fc) + data
    s.sendall(packet)
    
    resp_header = s.recv(7)
    if len(resp_header) < 7:
        raise ConnectionError("Connection closed by remote host")
    tid, pid, length, uid = struct.unpack('>HHHB', resp_header)
    
    resp_data = s.recv(length - 1)
    if len(resp_data) < 1:
        raise ConnectionError("Truncated response received")
    
    resp_fc = resp_data[0]
    if resp_fc & 0x80:
        raise Exception(f"Modbus exception: code {resp_data[1]}")
        
    return resp_data[1:]

def read_coils(s, address, count):
    data = struct.pack('>HH', address, count)
    payload = send_modbus_raw(s, 1, 1, data)
    byte_count = payload[0]
    coils_bytes = payload[1:]
    res = []
    for i in range(count):
        byte_idx = i // 8
        bit_idx = i % 8
        res.append(bool((coils_bytes[byte_idx] >> bit_idx) & 1))
    return res

def read_discrete_inputs(s, address, count):
    data = struct.pack('>HH', address, count)
    payload = send_modbus_raw(s, 2, 1, data)
    byte_count = payload[0]
    inputs_bytes = payload[1:]
    res = []
    for i in range(count):
        byte_idx = i // 8
        bit_idx = i % 8
        res.append(bool((inputs_bytes[byte_idx] >> bit_idx) & 1))
    return res

def read_holding_registers(s, address, count):
    data = struct.pack('>HH', address, count)
    payload = send_modbus_raw(s, 3, 1, data)
    byte_count = payload[0]
    regs = []
    for i in range(count):
        val = struct.unpack('>H', payload[1 + i*2 : 3 + i*2])[0]
        regs.append(val)
    return regs

def read_input_registers(s, address, count):
    data = struct.pack('>HH', address, count)
    payload = send_modbus_raw(s, 4, 1, data)
    byte_count = payload[0]
    regs = []
    for i in range(count):
        val = struct.unpack('>H', payload[1 + i*2 : 3 + i*2])[0]
        regs.append(val)
    return regs

def write_single_coil(s, address, value):
    val_to_send = 0xFF00 if value else 0x0000
    data = struct.pack('>HH', address, val_to_send)
    send_modbus_raw(s, 5, 1, data)

def write_single_register(s, address, value):
    data = struct.pack('>HH', address, value)
    send_modbus_raw(s, 6, 1, data)

def write_multiple_registers(s, address, values):
    data = struct.pack('>HHB', address, len(values), len(values)*2)
    for v in values:
        data += struct.pack('>H', v)
    send_modbus_raw(s, 16, 1, data)

def read_input_float(s, address):
    regs = read_input_registers(s, address, 2)
    raw_bits = (regs[0] << 16) | regs[1]
    pack_val = struct.pack('>I', raw_bits)
    return struct.unpack('>f', pack_val)[0]

def main():
    print("=== STARTING MODBUS TCP SIMULATION E2E VERIFICATION ===")
    
    proc = None
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.settimeout(2.0)
    try:
        s.connect(("127.0.0.1", 502))
        print("Connected to already running application on port 502")
    except Exception:
        print("Application is not running. Launching AvaloniaApplication1...")
        proc = subprocess.Popen(
            ["dotnet", "run", "--project", r"c:\Users\ess2\source\repos\AvaloniaApplication1\AvaloniaApplication1"],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True
        )
        
        for attempt in range(20):
            time.sleep(0.5)
            try:
                s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                s.settimeout(2.0)
                s.connect(("127.0.0.1", 502))
                print("Successfully connected to newly launched application on port 502!")
                break
            except Exception:
                pass
        else:
            print("Failed to start or connect to the application.")
            if proc:
                proc.terminate()
            return

    print("Waiting 1.5 seconds for simulation engine ticks to populate registers...")
    time.sleep(1.5)

    try:
        # 2. Verify Initial Ambient / Zero State
        temp_raw = read_input_registers(s, 0, 1)[0] / 10.0
        press_raw = read_input_registers(s, 5, 1)[0] / 100.0
        weight_raw = read_input_registers(s, 17, 1)[0] / 10.0
        
        temp_float = read_input_float(s, 22)
        press_float = read_input_float(s, 24)
        weight_float = read_input_float(s, 28)
        
        print(f"Initial State:")
        print(f"  Temp1:  {temp_float:.2f} °C (Raw register value: {temp_raw:.1f} °C)")
        print(f"  Press1: {press_float:.2f} bar (Raw register value: {press_raw:.2f} bar)")
        print(f"  Weight3: {weight_float:.2f} kg (Raw register value: {weight_raw:.1f} kg)")
        
        assert 15.0 <= temp_float <= 25.0, f"Expected initial Temp1 near ambient (15-25), got {temp_float}"
        assert 0.9 <= press_float <= 1.1, f"Expected initial Press1 near 1.0 bar, got {press_float}"
        assert weight_float <= 1.0, f"Expected initial Weight3 near 0 kg, got {weight_float}"
        print("[PASS] Initial State verification.")

        # 3. Test Water Dose Filling
        print("\n--- Test 1: Start Water Filling (Dose) ---")
        write_single_register(s, 4, 500)
        write_single_register(s, 1, 800)
        write_single_register(s, 0, 1000)
        
        write_single_coil(s, 0, True)
        write_single_coil(s, 1, True)
        print("Written controls: TargetWaterVol=500L, PumpSpeed=80%, ControlValve=100%, Start=True")
        
        print("Waiting 8.0 seconds for valve to open and flow to stabilize...")
        time.sleep(8.0)
        
        discretes = read_discrete_inputs(s, 0, 3)
        water_pump_fb = discretes[0]
        water_valve_fb = discretes[1]
        
        flow_in = read_input_registers(s, 10, 1)[0] / 10.0
        tot_val = read_input_float(s, 34)
        weight_mixer = read_input_float(s, 28)
        
        print(f"Water dosing state after 8s:")
        print(f"  Water Pump Feedback:  {water_pump_fb}")
        print(f"  Water Valve Feedback: {water_valve_fb}")
        print(f"  Water Flow In:        {flow_in:.1f} L/min")
        print(f"  Totalizer:            {tot_val:.2f} L")
        print(f"  Mixer Tank Weight:    {weight_mixer:.2f} kg")
        
        assert water_pump_fb == True, "Expected water pump feedback to be True"
        assert water_valve_fb == True, "Expected water valve feedback to be True"
        assert flow_in > 50.0, f"Expected active inlet flow, got {flow_in} L/min"
        assert tot_val > 1.0, f"Expected totalizer to increment, got {tot_val} L"
        assert weight_mixer > 1.0, f"Expected tank weight to increment, got {weight_mixer} kg"
        print("[PASS] Water Filling verification.")

        # 4. Test Heating
        print("\n--- Test 2: Start Heating ---")
        write_single_register(s, 2, 1000)
        write_single_coil(s, 5, True)
        print("Written controls: HeaterEnable=True, HeaterPower=100%")
        
        initial_temp = read_input_float(s, 22)
        print(f"  Initial heating temperature: {initial_temp:.2f} °C")
        
        time.sleep(3.0)
        
        current_temp = read_input_float(s, 22)
        heater_fb = read_discrete_inputs(s, 5, 1)[0]
        print(f"Heating state after 3s:")
        print(f"  Current Temp:    {current_temp:.2f} °C")
        print(f"  Heater Feedback: {heater_fb}")
        
        assert heater_fb == True, "Expected heater feedback to be True"
        assert current_temp > initial_temp, f"Expected temperature to rise, but went from {initial_temp} to {current_temp}"
        print("[PASS] Heating verification.")

        # 5. Test Reset Totalizer
        print("\n--- Test 3: Reset Totalizer ---")
        write_single_coil(s, 0, False)
        write_single_coil(s, 1, False)
        print("Written controls: Pump/Valve=False. Waiting 6.0s for flow to completely stop...")
        time.sleep(6.0)
        
        write_single_coil(s, 9, True)
        print("Written controls: ResetTotalizer=True")
        
        time.sleep(1.0)
        
        reset_tot_val = read_input_float(s, 34)
        reset_weight = read_input_float(s, 28)
        print(f"State after reset:")
        print(f"  Totalizer:         {reset_tot_val:.2f} L")
        print(f"  Mixer Tank Weight: {reset_weight:.2f} kg")
        
        assert reset_tot_val < 0.1, f"Expected totalizer to reset to 0, got {reset_tot_val}"
        assert reset_weight < 0.1, f"Expected mixer weight to reset to 0, got {reset_weight}"
        print("[PASS] Reset verification.")
        
        print("\n=== ALL SIMULATION VERIFICATION TESTS PASSED SUCCESSFULLY! ===")

    finally:
        s.close()
        if proc:
            print("Terminating background application process...")
            proc.terminate()
            proc.wait()
            print("Process terminated.")

if __name__ == "__main__":
    main()
