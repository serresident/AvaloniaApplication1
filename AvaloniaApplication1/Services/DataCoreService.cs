using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services.Protocols;

namespace AvaloniaApplication1.Services
{
    public class DataCoreService : IDataCoreService, IMockDataService, IDisposable
    {
        private readonly IConfigurationService _configService;
        private readonly ConcurrentDictionary<string, IProtocolDriver> _drivers = new();
        private readonly ConcurrentDictionary<string, object> _currentValuesCache = new();
        
        // The unified event aggregator stream for all widgets
        private readonly Subject<TagData> _unifiedTagStream = new();
        public IObservable<TagData> TagUpdates => _unifiedTagStream;

        private CancellationTokenSource? _cts;

        public DataCoreService(IConfigurationService configService)
        {
            _configService = configService;
        }

        public void Start()
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();

            Task.Run(async () =>
            {
                try
                {
                    var config = await _configService.LoadConfigurationAsync();
                    if (config?.Connections == null) return;

                    var allTags = ExtractAllTags(config);

                    foreach (var conn in config.Connections)
                    {
                        IProtocolDriver driver;

                        if (conn.Type == "MQTT")
                        {
                            var mqttTopics = allTags.Where(t => t.ConnId == conn.Id && t.Address != null).Select(t => t.Address!).Distinct();
                            driver = new MqttProtocolDriver(conn, mqttTopics);
                        }
                        else if (conn.Type == "ModbusTCP" || conn.Type == "ModbusRTUOverTCP")
                        {
                            var modbusTags = allTags.Where(t => t.ConnId == conn.Id);
                            driver = new ModbusProtocolDriver(conn, modbusTags);
                        }
                        else
                        {
                            Console.WriteLine($"Unknown connection type {conn.Type} for {conn.Id}");
                            continue;
                        }

                        _drivers[conn.Id] = driver;

                        // Route all driver events into the unified stream
                        driver.TagUpdates.Subscribe(tag => 
                        {
                            var key = $"{tag.ConnId}_{tag.Address}";
                            _currentValuesCache[key] = tag.Value;
                            _unifiedTagStream.OnNext(tag);
                        });

                        await driver.StartAsync(_cts.Token);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"DataCoreService Start Failed: {ex.Message}");
                }
            });
        }

        public void StartSimulation()
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();

            var driver = new MockProtocolDriver("mqtt1"); // Primary mock connection ID
            
            _drivers[driver.ConnectionId] = driver;
            
            driver.TagUpdates.Subscribe(tag => 
            {
                var key = $"{tag.ConnId}_{tag.Address}";
                _currentValuesCache[key] = tag.Value;
                _unifiedTagStream.OnNext(tag);
            });

            _ = driver.StartAsync(_cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            foreach (var driver in _drivers.Values)
            {
                driver.StopAsync().Wait();
                driver.Dispose();
            }
            _drivers.Clear();
            _currentValuesCache.Clear();
        }

        public void StopSimulation() => Stop();

        public void WriteCommand(string connId, string address, object value)
        {
            // Optimistic update
            var key = $"{connId}_{address}";
            _currentValuesCache[key] = value;
            _unifiedTagStream.OnNext(new TagData(connId, address, value));

            if (_drivers.TryGetValue(connId, out var driver))
            {
                // Fire and forget Command Bus
                _ = Task.Run(() => driver.WriteAsync(address, value, CancellationToken.None));
            }
        }

        public object? GetCurrentValue(string connId, string address)
        {
            var key = $"{connId}_{address}";
            return _currentValuesCache.TryGetValue(key, out var val) ? val : null;
        }

        public void Dispose()
        {
            Stop();
            _unifiedTagStream.Dispose();
        }

        private IEnumerable<DataSourceConfig> ExtractAllTags(HmiConfiguration config)
        {
            var list = new List<DataSourceConfig>();

            void Scan(IEnumerable<WidgetConfig>? widgets)
            {
                if (widgets == null) return;
                foreach (var w in widgets)
                {
                    if (w.Source != null) list.Add(w.Source);
                    Scan(w.Children);
                }
            }

            Scan(config.Dashboard?.Widgets);
            Scan(config.Mimic?.Widgets);
            
            return list;
        }
    }
}
