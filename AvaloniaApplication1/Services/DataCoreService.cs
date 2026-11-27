using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services.Protocols;

namespace AvaloniaApplication1.Services
{
    public class DataCoreService : IDataCoreService, IDisposable, IAsyncDisposable
    {
        private readonly IConfigurationService _configService;
        private readonly IProtocolDriverFactory _driverFactory;
        private readonly ITagManagerService _tagManager;
        private readonly IAlarmNotificationService? _alarmService;
        private readonly ConcurrentDictionary<string, IProtocolDriver> _drivers = new();
        private readonly ConcurrentDictionary<string, object> _currentValuesCache = new();
        
        public ITagManagerService TagManager => _tagManager;

        // The unified event aggregator stream for raw tag data (synchronized for multi-driver thread safety)
        private readonly Subject<TagData> _rawUnifiedTagStream = new();
        private readonly ISubject<TagData> _unifiedTagStream;
        public IObservable<TagData> TagUpdates => _unifiedTagStream;

        // High-level snapshot updates from TagManager (with Quality & Overridden flags)
        public IObservable<TagSnapshot> SnapshotUpdates => _tagManager.SnapshotUpdates;

        public int TotalDriverCount => _drivers.Count;
        public int ConnectedDriverCount => _drivers.Values.Count(d => d.IsConnected);

        public IReadOnlyDictionary<string, bool> GetDriverConnectionStates()
        {
            return _drivers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.IsConnected);
        }

        private CancellationTokenSource? _cts;
        private CompositeDisposable _disposables = new();

        public DataCoreService(
            IConfigurationService configService, 
            IProtocolDriverFactory driverFactory,
            ITagManagerService? tagManager = null,
            IAlarmNotificationService? alarmService = null)
        {
            _configService = configService;
            _driverFactory = driverFactory;
            _tagManager = tagManager ?? new TagManagerService();
            _alarmService = alarmService;
            _unifiedTagStream = Subject.Synchronize(_rawUnifiedTagStream);
        }

        public async Task StartAsync()
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();
            _disposables = new CompositeDisposable();

            try
            {
                var config = await _configService.LoadConfigurationAsync();
                if (config == null) return;

                // Initialize TagManager with config tags (mark MQTT tags as OnChange watchdog)
                if (config.Tags != null && config.Tags.Count > 0)
                {
                    var mqttConnIds = new HashSet<string>(
                        (config.Connections ?? Enumerable.Empty<ConnectionConfig>())
                        .Where(c => string.Equals(c.Type, "MQTT", StringComparison.OrdinalIgnoreCase))
                        .Select(c => c.Id),
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var tag in config.Tags)
                    {
                        if (mqttConnIds.Contains(tag.ConnId))
                        {
                            tag.WatchdogMode = TagWatchdogMode.OnChange;
                        }
                    }

                    _tagManager.Initialize(config.Tags);
                }

                // Bridge TagManager snapshot quality changes back into the raw TagUpdates stream for backward compatibility
                _tagManager.SnapshotUpdates
                    .Subscribe(snapshot =>
                    {
                        var tagDef = _tagManager.GetTagDefinition(snapshot.TagId);
                        if (tagDef != null && !string.IsNullOrEmpty(tagDef.ConnId) && !string.IsNullOrEmpty(tagDef.Address))
                        {
                            var currentVal = snapshot.Value ?? GetCurrentValue(tagDef.ConnId, tagDef.Address) ?? 0;
                            _unifiedTagStream.OnNext(new TagData(tagDef.ConnId, tagDef.Address, currentVal, snapshot.Quality, snapshot.Timestamp));
                        }
                    })
                    .DisposeWith(_disposables);

                if (config.Connections == null) return;

                var allTags = ExtractAllTags(config).ToList();

                foreach (var conn in config.Connections)
                {
                    var tagsForConn = allTags.Where(t => t.ConnId == conn.Id);
                    var driver = _driverFactory.CreateDriver(conn, tagsForConn);

                    if (driver == null)
                    {
                        Console.WriteLine($"Unknown or unsupported connection type {conn.Type} for {conn.Id}");
                        continue;
                    }

                    _drivers[conn.Id] = driver;

                    // Route all driver events into the unified stream and TagManager safely
                    driver.TagUpdates
                        .Subscribe(tag => PublishTag(tag))
                        .DisposeWith(_disposables);

                    // Background start
                    driver.StartAsync(_cts.Token).FireAndForget(context: $"Driver.{conn.Id}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DataCoreService Start Failed: {ex.Message}");
            }
        }

        public void PublishTag(TagData tag)
        {
            var rawOrFallback = tag.Value ?? GetCurrentValue(tag.ConnId, tag.Address) ?? 0;
            _tagManager.UpdateTagValue(tag.ConnId, tag.Address, rawOrFallback, tag.Quality);

            var effective = _tagManager.GetSnapshotByAddress(tag.ConnId, tag.Address);
            var key = $"{tag.ConnId}_{tag.Address}";
            var valueToCache = effective.Quality != TagQuality.Uninitialized && effective.Value != null
                ? effective.Value
                : tag.Value;
            if (valueToCache != null)
            {
                _currentValuesCache[key] = valueToCache;
            }
        }

        public async Task StopAsync()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            _disposables.Dispose();

            var stopTasks = _drivers.Values.Select(driver => driver.StopAsync());
            await Task.WhenAll(stopTasks);

            foreach (var driver in _drivers.Values)
            {
                driver.Dispose();
            }
            _drivers.Clear();
            _currentValuesCache.Clear();
            _tagManager.Clear();
        }

        public void WriteCommand(string connId, string address, object value)
        {
            if (_drivers.TryGetValue(connId, out var driver))
            {
                ExecuteDriverWriteAsync(driver, connId, address, value)
                    .FireAndForget(context: $"WriteCommand.{connId}");
            }
            else
            {
                // Local / Simulation fallback when no physical protocol driver is attached
                PublishTag(new TagData(connId, address, value));
            }
        }

        private async Task ExecuteDriverWriteAsync(IProtocolDriver driver, string connId, string address, object value)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await driver.WriteAsync(address, value, cts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DataCore] WriteCommand failed for {connId}:{address} -> {ex.Message}");
                _alarmService?.ShowAlarm("Ошибка записи команды", $"Не удалось записать {value} в {connId}:{address}: {ex.Message}");
            }
        }

        public object? GetCurrentValue(string connId, string address)
        {
            var snapshot = _tagManager.GetSnapshotByAddress(connId, address);
            if (snapshot.Quality != TagQuality.Uninitialized && snapshot.Value != null)
            {
                return snapshot.Value;
            }

            var key = $"{connId}_{address}";
            return _currentValuesCache.TryGetValue(key, out var val) ? val : null;
        }

        public TagSnapshot GetTagSnapshot(string tagId)
        {
            return _tagManager.GetSnapshot(tagId);
        }

        public TagSnapshot GetTagSnapshot(string connId, string address)
        {
            return _tagManager.GetSnapshotByAddress(connId, address);
        }

        public void ForceOverride(string tagId, object value)
        {
            _tagManager.ForceOverride(tagId, value);
            var tag = _tagManager.GetTagDefinition(tagId);
            if (tag != null && !string.IsNullOrEmpty(tag.ConnId) && !string.IsNullOrEmpty(tag.Address))
            {
                WriteCommand(tag.ConnId, tag.Address, value);
            }
        }

        public void ClearOverride(string tagId)
        {
            _tagManager.ClearOverride(tagId);
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _disposables.Dispose();
            foreach (var driver in _drivers.Values)
            {
                driver.Dispose();
            }
            _drivers.Clear();
            _currentValuesCache.Clear();
            _tagManager.Dispose();
            _rawUnifiedTagStream.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _tagManager.Dispose();
            _rawUnifiedTagStream.Dispose();
        }

        private IEnumerable<DataSourceConfig> ExtractAllTags(HmiConfiguration config)
        {
            var list = new List<DataSourceConfig>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddSource(DataSourceConfig? src)
            {
                if (src == null) return;
                string conn = src.ConnId;
                string addr = src.Address;

                // If address is empty but TagId is specified, look up in config.Tags
                if (string.IsNullOrEmpty(addr) && !string.IsNullOrEmpty(src.TagId) && config.Tags != null)
                {
                    var found = config.Tags.FirstOrDefault(t => string.Equals(t.TagId, src.TagId, StringComparison.OrdinalIgnoreCase));
                    if (found != null)
                    {
                        conn = found.ConnId;
                        addr = found.Address;
                        src.ConnId = conn;
                        src.Address = addr;
                        if (string.IsNullOrEmpty(src.DataType)) src.DataType = found.DataType;
                    }
                }

                if (string.IsNullOrEmpty(conn) || string.IsNullOrEmpty(addr)) return;

                var key = $"{conn}:{addr}";
                if (seenKeys.Add(key))
                {
                    list.Add(src);
                }
            }

            void Scan(IEnumerable<WidgetConfig>? widgets)
            {
                if (widgets == null) return;
                foreach (var w in widgets)
                {
                    AddSource(w.Source);

                    if (w is ValveConfig vc)
                    {
                        AddSource(vc.FeedbackSource);
                        AddSource(vc.ClosedFeedbackSource);
                        AddSource(vc.ModeSource);
                    }
                    else if (w is PumpConfig pc)
                    {
                        AddSource(pc.ModeSource);
                        AddSource(pc.FeedbackSource);
                        AddSource(pc.SetpointSource);
                        AddSource(pc.FrequencyFeedbackSource);
                        AddSource(pc.CurrentFeedbackSource);
                        AddSource(pc.StatusWordSource);
                    }
                    else if (w is HeatExchangerConfig he)
                    {
                        AddSource(he.SecondarySource);
                    }
                    else if (w is ReactorConfig rc)
                    {
                        AddSource(rc.AgitatorSource);
                        AddSource(rc.TempSource);
                    }
                    else if (w is RealTimeTrendConfig rtc && rtc.Pens != null)
                    {
                        foreach (var pen in rtc.Pens)
                        {
                            AddSource(pen.Source);
                        }
                    }

                    if (w is ContainerButtonConfig cb && cb.Children != null) Scan(cb.Children);
                }
            }

            // 1. Scan Widgets in Dashboard & Mimic
            Scan(config.Dashboard?.Widgets);
            Scan(config.Mimic?.Widgets);

            // 2. Scan Centralized Tags in config.Tags
            if (config.Tags != null)
            {
                foreach (var tag in config.Tags)
                {
                    AddSource(new DataSourceConfig
                    {
                        TagId = tag.TagId,
                        ConnId = tag.ConnId,
                        Address = tag.Address,
                        DataType = tag.DataType
                    });
                }
            }

            // 3. Scan Bridge Rules
            if (config.BridgeRules != null)
            {
                foreach (var br in config.BridgeRules)
                {
                    if (br.Source != null)
                        AddSource(new DataSourceConfig { TagId = br.Source.TagId, ConnId = br.Source.ConnId, Address = br.Source.Address });
                    if (br.Destination != null)
                        AddSource(new DataSourceConfig { TagId = br.Destination.TagId, ConnId = br.Destination.ConnId, Address = br.Destination.Address });
                }
            }

            return list;
        }
    }
}
