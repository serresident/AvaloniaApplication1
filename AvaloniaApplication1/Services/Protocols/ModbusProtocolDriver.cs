using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Models.Config;
using NModbus;

namespace AvaloniaApplication1.Services.Protocols
{
    /// <summary>
    /// Industrial-grade Modbus protocol driver adapted from Rapid SCADA v6 (ScadaComm / DrvModbus).
    /// Features:
    /// 1. Single persistent TCP socket per connection (eliminates per-write socket churn and parallel port 502 collisions).
    /// 2. Prioritized Command Channel for writes (writes preempt the polling cycle immediately).
    /// 3. Contiguous Register Coalescing (MaxGap <= 5) & PDU Chunking (<= 120 registers).
    /// 4. Industrial TagQuality tracking (Good, CommFailure, Timeout).
    /// </summary>
    public class ModbusProtocolDriver : IProtocolDriver
    {
        public string ConnectionId { get; }
        private readonly ConnectionConfig _config;
        private readonly List<DataSourceConfig> _tagsToPoll;
        private readonly List<ModbusPollBlock> _coalescedBlocks = new();
        private readonly Subject<TagData> _tagUpdates = new();

        private readonly Channel<ModbusWriteRequest> _writeChannel =
            Channel.CreateUnbounded<ModbusWriteRequest>(new UnboundedChannelOptions { SingleReader = true });

        private CancellationTokenSource? _driverCts;

        public IObservable<TagData> TagUpdates => _tagUpdates;

        public ModbusProtocolDriver(ConnectionConfig config, IEnumerable<DataSourceConfig> tagsToPoll)
        {
            _config = config;
            ConnectionId = config.Id;

            // Deduplicate by address
            _tagsToPoll = tagsToPoll
                .Where(t => !string.IsNullOrEmpty(t.Address))
                .GroupBy(t => t.Address)
                .Select(g => g.First())
                .ToList();

            BuildCoalescedBlocks();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _driverCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task.Run(() => PersistentCommunicationLoopAsync(_driverCts.Token), _driverCts.Token)
                .FireAndForget(context: $"ModbusDriver.{ConnectionId}");
            return Task.CompletedTask;
        }

        public async Task WriteAsync(string address, object value, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var req = new ModbusWriteRequest(address, value, tcs, cancellationToken);

            if (!_writeChannel.Writer.TryWrite(req))
            {
                throw new InvalidOperationException($"Write channel for {ConnectionId} is closed.");
            }

            using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
            {
                await tcs.Task;
            }
        }

        public Task StopAsync()
        {
            _driverCts?.Cancel();
            _writeChannel.Writer.TryComplete();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _driverCts?.Cancel();
            _driverCts?.Dispose();
            _driverCts = null;
            _writeChannel.Writer.TryComplete();
            _tagUpdates.Dispose();
        }

        #region Communication & Polling Loop

        private async Task PersistentCommunicationLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient? tcpClient = null;
                IModbusMaster? master = null;

                try
                {
                    tcpClient = new TcpClient();
                    var connectTask = tcpClient.ConnectAsync(_config.Host, _config.Port);
                    var delayTask = Task.Delay(5000, ct);
                    var finishedTask = await Task.WhenAny(connectTask, delayTask);

                    if (finishedTask == delayTask || !tcpClient.Connected)
                    {
                        throw new SocketException((int)SocketError.TimedOut);
                    }

                    var factory = new ModbusFactory();
                    master = factory.CreateMaster(tcpClient);
                    master.Transport.ReadTimeout = 1500;
                    master.Transport.WriteTimeout = 1500;

                    Console.WriteLine($"[Modbus] Persistent master connected to {ConnectionId} ({_config.Host}:{_config.Port})");

                    while (!ct.IsCancellationRequested && tcpClient.Connected)
                    {
                        var cycleStart = DateTime.UtcNow;

                        // 1. DRAIN PENDING WRITES WITH ABSOLUTE PRIORITY
                        while (_writeChannel.Reader.TryRead(out var writeReq))
                        {
                            await ProcessWriteRequestAsync(master, writeReq);
                        }

                        // 2. BULK READ COALESCED BLOCKS
                        foreach (var block in _coalescedBlocks)
                        {
                            // Check for urgent writes between bulk reads
                            while (_writeChannel.Reader.TryRead(out var writeReq))
                            {
                                await ProcessWriteRequestAsync(master, writeReq);
                            }

                            if (ct.IsCancellationRequested || !tcpClient.Connected) break;

                            await ReadBlockAsync(master, block);
                        }

                        // 3. SLEEP / WAKE UP IMMEDIATELY ON WRITE
                        var elapsed = (int)(DateTime.UtcNow - cycleStart).TotalMilliseconds;
                        var sleepMs = Math.Max(20, _config.PollIntervalMs - elapsed);

                        var sleepDelay = Task.Delay(sleepMs, ct);
                        var writeWait = _writeChannel.Reader.WaitToReadAsync(ct).AsTask();
                        await Task.WhenAny(sleepDelay, writeWait);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Modbus] Connection/Comm failure on {ConnectionId}: {ex.Message}");
                    
                    // Mark all polled tags with CommFailure
                    foreach (var tag in _tagsToPoll)
                    {
                        _tagUpdates.OnNext(new TagData(ConnectionId, tag.Address, null!, TagQuality.CommFailure, DateTime.UtcNow));
                    }

                    // Fail any writes queued during connection drop
                    while (_writeChannel.Reader.TryRead(out var failedWrite))
                    {
                        failedWrite.CompletionSource.TrySetException(new IOException($"Connection to {_config.Host}:{_config.Port} lost."));
                    }

                    try
                    {
                        await Task.Delay(3000, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                finally
                {
                    master?.Dispose();
                    tcpClient?.Dispose();
                }
            }
        }

        private async Task ProcessWriteRequestAsync(IModbusMaster master, ModbusWriteRequest req)
        {
            if (req.CancellationToken.IsCancellationRequested)
            {
                req.CompletionSource.TrySetCanceled(req.CancellationToken);
                return;
            }

            try
            {
                if (ParseModbusAddress(req.Address, out byte fc, out ushort rawAddr))
                {
                    ushort offset = GetModbusOffset(fc, rawAddr);
                    byte slaveId = 1;

                    if (fc == 1 || fc == 2)
                    {
                        bool bVal = TagValueConverter.ToBool(req.Value);
                        await master.WriteSingleCoilAsync(slaveId, offset, bVal);
                    }
                    else
                    {
                        double dVal = TagValueConverter.ToDouble(req.Value);
                        var tag = _tagsToPoll.FirstOrDefault(t => t.Address == req.Address);
                        string dataType = tag?.DataType ?? "Float32";

                        if (dataType == "Double")
                        {
                            ulong bits = BitConverter.DoubleToUInt64Bits(dVal);
                            ushort w0 = (ushort)(bits >> 48);
                            ushort w1 = (ushort)((bits >> 32) & 0xFFFF);
                            ushort w2 = (ushort)((bits >> 16) & 0xFFFF);
                            ushort w3 = (ushort)(bits & 0xFFFF);

                            ushort[] words = _config.ByteOrder switch
                            {
                                "ABCD" => new[] { w0, w1, w2, w3 },
                                "CDAB" => new[] { w1, w0, w3, w2 },
                                "BADC" => new[] { SwapBytes(w0), SwapBytes(w1), SwapBytes(w2), SwapBytes(w3) },
                                _ => new[] { SwapBytes(w3), SwapBytes(w2), SwapBytes(w1), SwapBytes(w0) }
                            };
                            await master.WriteMultipleRegistersAsync(slaveId, offset, words);
                        }
                        else if (dataType == "Float32" || dataType == "Float" || dataType == "Int32" || dataType == "UInt32")
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
                        else
                        {
                            ushort uReg = (ushort)(short)dVal;
                            await master.WriteSingleRegisterAsync(slaveId, offset, uReg);
                        }
                    }

                    req.CompletionSource.TrySetResult(true);
                }
                else
                {
                    req.CompletionSource.TrySetException(new FormatException($"Invalid Modbus address: {req.Address}"));
                }
            }
            catch (Exception ex)
            {
                req.CompletionSource.TrySetException(ex);
                throw;
            }
        }

        private async Task ReadBlockAsync(IModbusMaster master, ModbusPollBlock block)
        {
            try
            {
                if (block.FunctionCode == 3)
                {
                    ushort[] regs = await master.ReadHoldingRegistersAsync(block.SlaveId, block.StartOffset, block.RegCount);
                    PublishBlockRegisters(block, regs);
                }
                else if (block.FunctionCode == 4)
                {
                    ushort[] regs = await master.ReadInputRegistersAsync(block.SlaveId, block.StartOffset, block.RegCount);
                    PublishBlockRegisters(block, regs);
                }
                else if (block.FunctionCode == 1)
                {
                    bool[] coils = await master.ReadCoilsAsync(block.SlaveId, block.StartOffset, block.RegCount);
                    PublishBlockBits(block, coils);
                }
                else if (block.FunctionCode == 2)
                {
                    bool[] inputs = await master.ReadInputsAsync(block.SlaveId, block.StartOffset, block.RegCount);
                    PublishBlockBits(block, inputs);
                }
            }
            catch (SlaveException sex)
            {
                Console.WriteLine($"[Modbus] Slave Exception FC={block.FunctionCode} start={block.StartOffset} count={block.RegCount} Code={sex.SlaveExceptionCode}");
                foreach (var tag in block.Tags)
                {
                    _tagUpdates.OnNext(new TagData(ConnectionId, tag.Tag.Address, null!, TagQuality.Bad, DateTime.UtcNow));
                }
                // Do NOT throw: Modbus exception response means TCP socket is alive and healthy!
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Modbus] Read comm error FC={block.FunctionCode} start={block.StartOffset} count={block.RegCount}: {ex.Message}");
                foreach (var tag in block.Tags)
                {
                    _tagUpdates.OnNext(new TagData(ConnectionId, tag.Tag.Address, null!, TagQuality.CommFailure, DateTime.UtcNow));
                }
                throw; // propagate network transport errors to communication loop for reconnect
            }
        }

        private void PublishBlockRegisters(ModbusPollBlock block, ushort[] rawRegs)
        {
            var rawSpan = rawRegs.AsSpan();
            foreach (var tagMapping in block.Tags)
            {
                int offsetIndex = tagMapping.Offset - block.StartOffset;
                if (offsetIndex < 0 || offsetIndex + tagMapping.RegCount > rawRegs.Length) continue;

                var slice = rawSpan.Slice(offsetIndex, tagMapping.RegCount);
                var val = ParseRegisters(slice, tagMapping.Tag.DataType, _config.ByteOrder);
                if (val != null)
                {
                    _tagUpdates.OnNext(new TagData(ConnectionId, tagMapping.Tag.Address, val, TagQuality.Good, DateTime.UtcNow));
                }
            }
        }

        private void PublishBlockBits(ModbusPollBlock block, bool[] bits)
        {
            foreach (var tagMapping in block.Tags)
            {
                int offsetIndex = tagMapping.Offset - block.StartOffset;
                if (offsetIndex < 0 || offsetIndex >= bits.Length) continue;

                bool val = bits[offsetIndex];
                _tagUpdates.OnNext(new TagData(ConnectionId, tagMapping.Tag.Address, val, TagQuality.Good, DateTime.UtcNow));
            }
        }

        #endregion

        #region Coalescing & Batching Algorithm

        /// <summary>
        /// Groups adjacent and nearby registers into bulk PDU requests according to Rapid SCADA DrvModbus rules:
        /// 1. Coalesces registers with gap <= 5 registers (MaxGap <= 5).
        /// 2. Splits into multiple PDUs when block size exceeds 120 registers (<= 120 registers).
        /// </summary>
        private void BuildCoalescedBlocks()
        {
            _coalescedBlocks.Clear();

            var tagMappings = new List<ModbusTagMapping>();
            foreach (var tag in _tagsToPoll)
            {
                if (ParseModbusAddress(tag.Address, out byte fc, out ushort rawAddr))
                {
                    ushort offset = GetModbusOffset(fc, rawAddr);
                    ushort regCount = GetRegisterCount(fc, tag.DataType);
                    tagMappings.Add(new ModbusTagMapping(tag, offset, regCount, fc));
                }
            }

            // Group by Function Code and Slave ID (1)
            var groups = tagMappings.GroupBy(m => m.FunctionCode);

            foreach (var group in groups)
            {
                byte fc = group.Key;
                var sorted = group.OrderBy(m => m.Offset).ToList();

                ModbusPollBlock? currentBlock = null;

                foreach (var item in sorted)
                {
                    if (currentBlock == null)
                    {
                        currentBlock = new ModbusPollBlock
                        {
                            SlaveId = 1,
                            FunctionCode = fc,
                            StartOffset = item.Offset,
                            RegCount = item.RegCount
                        };
                        currentBlock.Tags.Add(item);
                        continue;
                    }

                    ushort currentEnd = (ushort)(currentBlock.StartOffset + currentBlock.RegCount);
                    ushort itemEnd = (ushort)(item.Offset + item.RegCount);

                    if (item.Offset >= currentBlock.StartOffset)
                    {
                        int gap = item.Offset - currentEnd;
                        int newTotalSpan = Math.Max(currentEnd, itemEnd) - currentBlock.StartOffset;

                        // MaxGap <= 5 registers, and total PDU block size <= 120 registers
                        if (gap <= 5 && newTotalSpan <= 120)
                        {
                            currentBlock.RegCount = (ushort)newTotalSpan;
                            currentBlock.Tags.Add(item);
                            continue;
                        }
                    }

                    _coalescedBlocks.Add(currentBlock);
                    currentBlock = new ModbusPollBlock
                    {
                        SlaveId = 1,
                        FunctionCode = fc,
                        StartOffset = item.Offset,
                        RegCount = item.RegCount
                    };
                    currentBlock.Tags.Add(item);
                }

                if (currentBlock != null)
                {
                    _coalescedBlocks.Add(currentBlock);
                }
            }

            Console.WriteLine($"[Modbus] Driver '{ConnectionId}' coalesced {_tagsToPoll.Count} tags into {_coalescedBlocks.Count} bulk PDU blocks.");
        }

        public IReadOnlyList<ModbusPollBlock> CoalescedBlocks => _coalescedBlocks.AsReadOnly();

        private static ushort GetRegisterCount(byte functionCode, string? dataType)
        {
            if (functionCode == 1 || functionCode == 2) return 1; // Bits/Coils
            if (dataType == "Float32" || dataType == "Float" || dataType == "Int32" || dataType == "UInt32") return 2;
            if (dataType == "Double") return 4;
            return 1;
        }

        #endregion

        #region Address Parsing & Decoding

        public static bool ParseModbusAddress(string address, out byte functionCode, out ushort startAddress)
        {
            functionCode = 3;
            startAddress = 0;

            if (string.IsNullOrWhiteSpace(address)) return false;

            address = address.Trim();

            // 5-digit Modbus convention (0xxxx = coils, 1xxxx = discrete inputs, 3xxxx = input regs, 4xxxx = holding regs)
            if (address.Length >= 5 && uint.TryParse(address, out uint fullAddr))
            {
                if (fullAddr >= 40001 && fullAddr <= 49999)
                {
                    functionCode = 3;
                    startAddress = (ushort)fullAddr;
                    return true;
                }
                if (fullAddr >= 30001 && fullAddr <= 39999)
                {
                    functionCode = 4;
                    startAddress = (ushort)fullAddr;
                    return true;
                }
                if (fullAddr >= 10001 && fullAddr <= 19999)
                {
                    functionCode = 2;
                    startAddress = (ushort)fullAddr;
                    return true;
                }
                if (address.StartsWith("0") && fullAddr >= 1 && fullAddr <= 9999)
                {
                    functionCode = 1;
                    startAddress = (ushort)fullAddr;
                    return true;
                }
            }

            // Direct coil address (e.g. "0", "00", "00001")
            if (address.StartsWith("0") && ushort.TryParse(address, out ushort coilAddr))
            {
                functionCode = 1;
                startAddress = coilAddr;
                return true;
            }

            // Direct offset (default Holding Register FC 3)
            if (ushort.TryParse(address, out ushort directAddr))
            {
                functionCode = 3;
                startAddress = directAddr;
                return true;
            }

            return false;
        }

        public static ushort GetModbusOffset(byte functionCode, ushort address)
        {
            switch (functionCode)
            {
                case 1: return (ushort)(address >= 1 ? address - 1 : address);
                case 2: return (ushort)(address >= 10001 ? address - 10001 : address);
                case 3: return (ushort)(address >= 40001 ? address - 40001 : address);
                case 4: return (ushort)(address >= 30001 ? address - 30001 : address);
                default: return address;
            }
        }

        private static ushort SwapBytes(ushort value) => (ushort)((value >> 8) | (value << 8));

        private static object ParseRegisters(ReadOnlySpan<ushort> regs, string dataType, string byteOrder)
        {
            if (regs.Length == 1)
            {
                if (dataType == "Int16") return (short)regs[0];
                if (dataType == "Bool") return regs[0] > 0;
                return regs[0];
            }

            if (regs.Length >= 4 && dataType == "Double")
            {
                ushort r0 = regs[0], r1 = regs[1], r2 = regs[2], r3 = regs[3];
                ulong val = byteOrder switch
                {
                    "ABCD" => ((ulong)r0 << 48) | ((ulong)r1 << 32) | ((ulong)r2 << 16) | r3,
                    "CDAB" => ((ulong)r1 << 48) | ((ulong)r0 << 32) | ((ulong)r3 << 16) | r2,
                    "BADC" => ((ulong)SwapBytes(r0) << 48) | ((ulong)SwapBytes(r1) << 32) | ((ulong)SwapBytes(r2) << 16) | SwapBytes(r3),
                    _ => ((ulong)SwapBytes(r3) << 48) | ((ulong)SwapBytes(r2) << 32) | ((ulong)SwapBytes(r1) << 16) | SwapBytes(r0) // DCBA
                };
                return BitConverter.UInt64BitsToDouble(val);
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

        #endregion

        #region Internal Types

        private sealed record ModbusWriteRequest(
            string Address,
            object Value,
            TaskCompletionSource<bool> CompletionSource,
            CancellationToken CancellationToken
        );

        public sealed record ModbusTagMapping(
            DataSourceConfig Tag,
            ushort Offset,
            ushort RegCount,
            byte FunctionCode = 3
        );

        public sealed class ModbusPollBlock
        {
            public byte SlaveId { get; set; } = 1;
            public byte FunctionCode { get; set; } = 3;
            public ushort StartOffset { get; set; }
            public ushort RegCount { get; set; }
            public List<ModbusTagMapping> Tags { get; } = new();
        }

        #endregion
    }
}
