using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NModbus;

namespace AvaloniaApplication1.Services
{
    public enum ByteOrder
    {
        ABCD, // Big Endian
        CDAB, // Word Swap / Mid-Little Endian
        BADC, // Byte Swap
        DCAB  // Little Endian
    }

    public class ModbusServerManager
    {
        private readonly ModbusFactory _factory;
        private TcpListener? _listener;
        private IModbusSlaveNetwork? _slaveNetwork;
        private IModbusSlave? _slave;
        private CancellationTokenSource? _cts;

        public bool IsRunning { get; private set; }
        public int Port { get; private set; } = 502;
        public byte SlaveId { get; private set; } = 1;
        public ByteOrder CurrentByteOrder { get; set; } = ByteOrder.ABCD;

        public ModbusServerManager()
        {
            _factory = new ModbusFactory();
        }

        public void Start(int port, byte slaveId)
        {
            if (IsRunning)
            {
                Stop();
            }

            Port = port;
            SlaveId = slaveId;

            try
            {
                // Bind to localhost 127.0.0.1 as required
                _listener = new TcpListener(IPAddress.Loopback, port);
                _listener.Start();

                _slaveNetwork = _factory.CreateSlaveNetwork(_listener);
                _slave = _factory.CreateSlave(slaveId);
                _slaveNetwork.AddSlave(_slave);

                // Initialize registers up to index 100 to avoid out-of-bounds client requests
                _slave.DataStore.HoldingRegisters.WritePoints(1, new ushort[100]);
                _slave.DataStore.InputRegisters.WritePoints(1, new ushort[100]);
                _slave.DataStore.CoilDiscretes.WritePoints(1, new bool[100]);
                _slave.DataStore.CoilInputs.WritePoints(1, new bool[100]);

                _cts = new CancellationTokenSource();
                // NModbus v3 requires calling ListenAsync to accept connections in the background
                _ = _slaveNetwork.ListenAsync(_cts.Token);

                IsRunning = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ModbusServerManager Start failed: {ex.Message}");
                Stop();
                throw;
            }
        }

        public void Stop()
        {
            IsRunning = false;
            
            try
            {
                _cts?.Cancel();
            }
            catch { }
            finally
            {
                _cts = null;
            }

            try
            {
                _slaveNetwork?.Dispose();
            }
            catch { }
            finally
            {
                _slaveNetwork = null;
                _slave = null;
            }

            try
            {
                _listener?.Stop();
            }
            catch { }
            finally
            {
                _listener = null;
            }
        }

        // --- Coils (0x) ---
        public bool ReadCoil(ushort address)
        {
            if (_slave == null) return false;
            lock (_slave.DataStore)
            {
                return _slave.DataStore.CoilDiscretes.ReadPoints(address, 1)[0];
            }
        }

        public bool[] ReadCoils(ushort address, ushort count)
        {
            if (_slave == null) return new bool[count];
            lock (_slave.DataStore)
            {
                return _slave.DataStore.CoilDiscretes.ReadPoints(address, count);
            }
        }

        public void WriteCoil(ushort address, bool value)
        {
            if (_slave == null) return;
            lock (_slave.DataStore)
            {
                _slave.DataStore.CoilDiscretes.WritePoints(address, new[] { value });
            }
        }

        public void WriteCoils(ushort address, bool[] values)
        {
            if (_slave == null) return;
            lock (_slave.DataStore)
            {
                _slave.DataStore.CoilDiscretes.WritePoints(address, values);
            }
        }

        // --- Discrete Inputs (1x) ---
        public bool ReadDiscreteInput(ushort address)
        {
            if (_slave == null) return false;
            lock (_slave.DataStore)
            {
                return _slave.DataStore.CoilInputs.ReadPoints(address, 1)[0];
            }
        }

        public bool[] ReadDiscreteInputs(ushort address, ushort count)
        {
            if (_slave == null) return new bool[count];
            lock (_slave.DataStore)
            {
                return _slave.DataStore.CoilInputs.ReadPoints(address, count);
            }
        }

        public void WriteDiscreteInput(ushort address, bool value)
        {
            if (_slave == null) return;
            lock (_slave.DataStore)
            {
                _slave.DataStore.CoilInputs.WritePoints(address, new[] { value });
            }
        }

        public void WriteDiscreteInputs(ushort address, bool[] values)
        {
            if (_slave == null) return;
            lock (_slave.DataStore)
            {
                _slave.DataStore.CoilInputs.WritePoints(address, values);
            }
        }

        // --- Input Registers (3x) ---
        public ushort ReadInputRegister(ushort address)
        {
            if (_slave == null) return 0;
            lock (_slave.DataStore)
            {
                return _slave.DataStore.InputRegisters.ReadPoints(address, 1)[0];
            }
        }

        public ushort[] ReadInputRegisters(ushort address, ushort count)
        {
            if (_slave == null) return new ushort[count];
            lock (_slave.DataStore)
            {
                return _slave.DataStore.InputRegisters.ReadPoints(address, count);
            }
        }

        public void WriteInputRegister(ushort address, ushort value)
        {
            if (_slave == null) return;
            lock (_slave.DataStore)
            {
                _slave.DataStore.InputRegisters.WritePoints(address, new[] { value });
            }
        }

        public void WriteInputRegisters(ushort address, ushort[] values)
        {
            if (_slave == null) return;
            lock (_slave.DataStore)
            {
                _slave.DataStore.InputRegisters.WritePoints(address, values);
            }
        }

        // --- Holding Registers (4x) ---
        public ushort ReadHoldingRegister(ushort address)
        {
            if (_slave == null) return 0;
            lock (_slave.DataStore)
            {
                return _slave.DataStore.HoldingRegisters.ReadPoints(address, 1)[0];
            }
        }

        public ushort[] ReadHoldingRegisters(ushort address, ushort count)
        {
            if (_slave == null) return new ushort[count];
            lock (_slave.DataStore)
            {
                return _slave.DataStore.HoldingRegisters.ReadPoints(address, count);
            }
        }

        public void WriteHoldingRegister(ushort address, ushort value)
        {
            if (_slave == null) return;
            lock (_slave.DataStore)
            {
                _slave.DataStore.HoldingRegisters.WritePoints(address, new[] { value });
            }
        }

        public void WriteHoldingRegisters(ushort address, ushort[] values)
        {
            if (_slave == null) return;
            lock (_slave.DataStore)
            {
                _slave.DataStore.HoldingRegisters.WritePoints(address, values);
            }
        }

        // --- Configurable 32-bit Swap Write/Read (DInt / Float) ---
        public void WriteInput32(ushort address, byte[] bytes)
        {
            ushort reg0 = 0;
            ushort reg1 = 0;

            switch (CurrentByteOrder)
            {
                case ByteOrder.ABCD:
                    reg0 = (ushort)((bytes[3] << 8) | bytes[2]);
                    reg1 = (ushort)((bytes[1] << 8) | bytes[0]);
                    break;
                case ByteOrder.CDAB:
                    reg0 = (ushort)((bytes[1] << 8) | bytes[0]);
                    reg1 = (ushort)((bytes[3] << 8) | bytes[2]);
                    break;
                case ByteOrder.BADC:
                    reg0 = (ushort)((bytes[2] << 8) | bytes[3]);
                    reg1 = (ushort)((bytes[0] << 8) | bytes[1]);
                    break;
                case ByteOrder.DCAB:
                    reg0 = (ushort)((bytes[0] << 8) | bytes[1]);
                    reg1 = (ushort)((bytes[2] << 8) | bytes[3]);
                    break;
            }
            WriteInputRegisters(address, new[] { reg0, reg1 });
        }

        public byte[] ReadInput32(ushort address)
        {
            ushort[] regs = ReadInputRegisters(address, 2);
            ushort reg0 = regs[0];
            ushort reg1 = regs[1];
            
            byte[] bytes = new byte[4];
            switch (CurrentByteOrder)
            {
                case ByteOrder.ABCD:
                    bytes[3] = (byte)(reg0 >> 8);
                    bytes[2] = (byte)(reg0 & 0xFF);
                    bytes[1] = (byte)(reg1 >> 8);
                    bytes[0] = (byte)(reg1 & 0xFF);
                    break;
                case ByteOrder.CDAB:
                    bytes[1] = (byte)(reg0 >> 8);
                    bytes[0] = (byte)(reg0 & 0xFF);
                    bytes[3] = (byte)(reg1 >> 8);
                    bytes[2] = (byte)(reg1 & 0xFF);
                    break;
                case ByteOrder.BADC:
                    bytes[2] = (byte)(reg0 >> 8);
                    bytes[3] = (byte)(reg0 & 0xFF);
                    bytes[0] = (byte)(reg1 >> 8);
                    bytes[1] = (byte)(reg1 & 0xFF);
                    break;
                case ByteOrder.DCAB:
                    bytes[0] = (byte)(reg0 >> 8);
                    bytes[1] = (byte)(reg0 & 0xFF);
                    bytes[2] = (byte)(reg1 >> 8);
                    bytes[3] = (byte)(reg1 & 0xFF);
                    break;
            }
            return bytes;
        }

        public void WriteHolding32(ushort address, byte[] bytes)
        {
            ushort reg0 = 0;
            ushort reg1 = 0;

            switch (CurrentByteOrder)
            {
                case ByteOrder.ABCD:
                    reg0 = (ushort)((bytes[3] << 8) | bytes[2]);
                    reg1 = (ushort)((bytes[1] << 8) | bytes[0]);
                    break;
                case ByteOrder.CDAB:
                    reg0 = (ushort)((bytes[1] << 8) | bytes[0]);
                    reg1 = (ushort)((bytes[3] << 8) | bytes[2]);
                    break;
                case ByteOrder.BADC:
                    reg0 = (ushort)((bytes[2] << 8) | bytes[3]);
                    reg1 = (ushort)((bytes[0] << 8) | bytes[1]);
                    break;
                case ByteOrder.DCAB:
                    reg0 = (ushort)((bytes[0] << 8) | bytes[1]);
                    reg1 = (ushort)((bytes[2] << 8) | bytes[3]);
                    break;
            }
            WriteHoldingRegisters(address, new[] { reg0, reg1 });
        }

        public byte[] ReadHolding32(ushort address)
        {
            ushort[] regs = ReadHoldingRegisters(address, 2);
            ushort reg0 = regs[0];
            ushort reg1 = regs[1];
            
            byte[] bytes = new byte[4];
            switch (CurrentByteOrder)
            {
                case ByteOrder.ABCD:
                    bytes[3] = (byte)(reg0 >> 8);
                    bytes[2] = (byte)(reg0 & 0xFF);
                    bytes[1] = (byte)(reg1 >> 8);
                    bytes[0] = (byte)(reg1 & 0xFF);
                    break;
                case ByteOrder.CDAB:
                    bytes[1] = (byte)(reg0 >> 8);
                    bytes[0] = (byte)(reg0 & 0xFF);
                    bytes[3] = (byte)(reg1 >> 8);
                    bytes[2] = (byte)(reg1 & 0xFF);
                    break;
                case ByteOrder.BADC:
                    bytes[2] = (byte)(reg0 >> 8);
                    bytes[3] = (byte)(reg0 & 0xFF);
                    bytes[0] = (byte)(reg1 >> 8);
                    bytes[1] = (byte)(reg1 & 0xFF);
                    break;
                case ByteOrder.DCAB:
                    bytes[0] = (byte)(reg0 >> 8);
                    bytes[1] = (byte)(reg0 & 0xFF);
                    bytes[2] = (byte)(reg1 >> 8);
                    bytes[3] = (byte)(reg1 & 0xFF);
                    break;
            }
            return bytes;
        }

        // --- Int32/Float wrappers ---
        public void WriteInputInt32(ushort address, int value) => WriteInput32(address, BitConverter.GetBytes(value));
        public int ReadInputInt32(ushort address) => BitConverter.ToInt32(ReadInput32(address), 0);
        public void WriteInputFloat(ushort address, float value) => WriteInput32(address, BitConverter.GetBytes(value));
        public float ReadInputFloat(ushort address) => BitConverter.ToSingle(ReadInput32(address), 0);

        public void WriteHoldingInt32(ushort address, int value) => WriteHolding32(address, BitConverter.GetBytes(value));
        public int ReadHoldingInt32(ushort address) => BitConverter.ToInt32(ReadHolding32(address), 0);
        public void WriteHoldingFloat(ushort address, float value) => WriteHolding32(address, BitConverter.GetBytes(value));
        public float ReadHoldingFloat(ushort address) => BitConverter.ToSingle(ReadHolding32(address), 0);
    }
}
