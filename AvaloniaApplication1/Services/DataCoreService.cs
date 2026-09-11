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
        private readonly ConcurrentDictionary<string, IProtocolDriver> _drivers = new();
        private readonly ConcurrentDictionary<string, object> _currentValuesCache = new();
        
        // The unified event aggregator stream for all widgets
        private readonly Subject<TagData> _unifiedTagStream = new();
        public IObservable<TagData> TagUpdates => _unifiedTagStream;

        private CancellationTokenSource? _cts;
        private CompositeDisposable _disposables = new();

        public DataCoreService(IConfigurationService configService, IProtocolDriverFactory driverFactory)
        {
            _configService = configService;
            _driverFactory = driverFactory;
        }

        public async Task StartAsync()
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();
            _disposables = new CompositeDisposable();

            try
            {
                var config = await _configService.LoadConfigurationAsync();
                if (config?.Connections == null) return;

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

                    // Route all driver events into the unified stream safely
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
            var key = $"{tag.ConnId}_{tag.Address}";
            _currentValuesCache[key] = tag.Value;
            _unifiedTagStream.OnNext(tag);
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
        }

        public void WriteCommand(string connId, string address, object value)
        {
            // Optimistic update
            PublishTag(new TagData(connId, address, value));

            if (_drivers.TryGetValue(connId, out var driver))
            {
                // Fire and forget Command Bus
                Task.Run(() => driver.WriteAsync(address, value, CancellationToken.None))
                    .FireAndForget(context: $"WriteCommand.{connId}");
            }
        }

        public object? GetCurrentValue(string connId, string address)
        {
            var key = $"{connId}_{address}";
            return _currentValuesCache.TryGetValue(key, out var val) ? val : null;
        }

        public void Dispose()
        {
            // Синхронная версия — безопасна только из non-async контекста (финализатор, тесты)
            _cts?.Cancel();
            _disposables.Dispose();
            foreach (var driver in _drivers.Values)
            {
                driver.Dispose();
            }
            _drivers.Clear();
            _currentValuesCache.Clear();
            _unifiedTagStream.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
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

                    if (w is ValveConfig vc)
                    {
                        if (vc.FeedbackSource != null) list.Add(vc.FeedbackSource);
                        if (vc.ModeSource != null) list.Add(vc.ModeSource);
                    }

                    if (w is ContainerButtonConfig cb && cb.Children != null) Scan(cb.Children);
                }
            }

            Scan(config.Dashboard?.Widgets);
            Scan(config.Mimic?.Widgets);
            
            return list;
        }
    }
}
