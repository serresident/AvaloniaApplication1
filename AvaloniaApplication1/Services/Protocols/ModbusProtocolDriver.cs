using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Models.Config;
using NModbus;

namespace AvaloniaApplication1.Services.Protocols
{
    public class ModbusProtocolDriver : IProtocolDriver
    {
        public string ConnectionId { get; }
        private readonly ConnectionConfig _config;
        private readonly IEnumerable<DataSourceConfig> _tagsToPoll;
        private readonly Subject<TagData> _tagUpdates = new();

        public IObservable<TagData> TagUpdates => _tagUpdates;

        public ModbusProtocolDriver(ConnectionConfig config, IEnumerable<DataSourceConfig> tagsToPoll)
        {
            _config = config;
            ConnectionId = config.Id;
            // Distinct by address to avoid polling the same register twice
            _tagsToPoll = tagsToPoll
                .Where(t => !string.IsNullOrEmpty(t.Address))
                .GroupBy(t => t.Address)
                .Select(g => g.First())
                .ToList();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Start the polling loop in the background
            Task.Run(() => PollModbusLoop(cancellationToken), cancellationToken).FireAndForget(context: "ModbusProtocolDriver");
            return Task.CompletedTask;
        }

        public async Task WriteAsync(string address, object value, CancellationToken cancellationToken)
        {
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(_config.Host, _config.Port);

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
                    var tag = _tagsToPoll.FirstOrDefault(t => t.Address == address);
                    string dataType = tag?.DataType ?? "Float32";

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
                        if (_config.ByteOrder == "ABCD")
                        {
                            r0 = (ushort)(bits >> 16);
                            r1 = (ushort)(bits & 0xFFFF);
                        }
                        else if (_config.ByteOrder == "CDAB")
                        {
                            r1 = (ushort)(bits >> 16);
                            r0 = (ushort)(bits & 0xFFFF);
                        }
                        else if (_config.ByteOrder == "BADC")
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

        public Task StopAsync()
        {
            // Polling loop will stop automatically when cancellation token is triggered
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _tagUpdates.Dispose();
        }

        private async Task PollModbusLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                using var tcpClient = new TcpClient();
                try
                {
                    var connectTask = tcpClient.ConnectAsync(_config.Host, _config.Port);
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

                    Console.WriteLine($"Modbus connected to {ConnectionId} ({_config.Host}:{_config.Port})");

                    while (!ct.IsCancellationRequested && tcpClient.Connected)
                    {
                        var startTime = DateTime.UtcNow;

                        foreach (var tag in _tagsToPoll)
                        {
                            try
                            {
                                if (ParseModbusAddress(tag.Address, out byte fc, out ushort rawAddr))
                                {
                                    ushort offset = GetModbusOffset(fc, rawAddr);
                                    byte slaveId = 1; // Default slave ID

                                    var value = ReadModbusValue(master, slaveId, fc, offset, tag.DataType, _config.ByteOrder);
                                    if (value != null)
                                    {
                                        _tagUpdates.OnNext(new TagData(ConnectionId, tag.Address, value));
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Modbus read error on tag {tag.Address}: {ex.Message}");
                            }
                        }

                        var elapsed = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                        var sleepTime = Math.Max(50, _config.PollIntervalMs - elapsed);
                        await Task.Delay(sleepTime, ct);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Modbus connection/polling failed for {ConnectionId}: {ex.Message}");
                    await Task.Delay(5000, ct);
                }
            }
        }

        private bool ParseModbusAddress(string address, out byte functionCode, out ushort startAddress)
        {
            functionCode = 3;
            startAddress = 0;

            if (string.IsNullOrEmpty(address)) return false;

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

            if (functionCode == 1) return master.ReadCoils(slaveId, offset, 1)[0];
            if (functionCode == 2) return master.ReadInputs(slaveId, offset, 1)[0];
            if (functionCode == 4) return ParseRegisters(master.ReadInputRegisters(slaveId, offset, (ushort)regCount), dataType, byteOrder);
            return ParseRegisters(master.ReadHoldingRegisters(slaveId, offset, (ushort)regCount), dataType, byteOrder);
        }

        private static ushort SwapBytes(ushort value) => (ushort)((value >> 8) | (value << 8));

        private object ParseRegisters(ushort[] regs, string dataType, string byteOrder)
        {
            if (regs.Length == 1)
            {
                if (dataType == "Int16") return (short)regs[0];
                if (dataType == "Bool") return regs[0] > 0;
                return regs[0];
            }

            if (regs.Length >= 2)
            {
                ushort r0 = regs[0], r1 = regs[1];
                uint val = byteOrder switch
                {
                    "ABCD" => ((uint)r0 << 16) | r1,
                    "CDAB" => ((uint)r1 << 16) | r0,
                    "BADC" => ((uint)SwapBytes(r0) << 16) | SwapBytes(r1),
                    _ => ((uint)SwapBytes(r1) << 16) | SwapBytes(r0) // DCBA
                };

                if (dataType == "Float32" || dataType == "Float") return BitConverter.UInt32BitsToSingle(val);
                if (dataType == "Int32") return (int)val;
                if (dataType == "UInt32") return val;
            }
            return regs[0];
        }
    }
}
