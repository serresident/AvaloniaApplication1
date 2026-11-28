using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive.Disposables;
using AvaloniaApplication1.Services.Protocols;
using AvaloniaApplication1.Models.Config;

namespace AvaloniaApplication1.Services
{
    public class SimulationService : ISimulationService, IDisposable, IAsyncDisposable
    {
        private readonly IDataCoreService _dataCore;
        private readonly IConfigurationService _configService;
        private readonly List<MockProtocolDriver> _backgroundSimulators = new();
        private CancellationTokenSource? _simCts;
        private readonly CompositeDisposable _simSubscriptions = new();
        private volatile bool _isPublishingFromSim;

        public SimulationService(IDataCoreService dataCore, IConfigurationService configService)
        {
            _dataCore = dataCore;
            _configService = configService;
        }

        public async Task StartSimulationAsync()
        {
            if (_simCts != null) return;
            _simCts = new CancellationTokenSource();

            var config = await _configService.LoadConfigurationAsync();
            if (config?.Connections != null)
            {
                foreach (var conn in config.Connections)
                {
                    var sim = new MockProtocolDriver(conn.Id);
                    
                    var sub = sim.TagUpdates.Subscribe(tag => 
                    {
                        _isPublishingFromSim = true;
                        try
                        {
                            _dataCore.PublishTag(tag);
                        }
                        finally
                        {
                            _isPublishingFromSim = false;
                        }
                    });
                    _simSubscriptions.Add(sub);
                    _backgroundSimulators.Add(sim);

                    sim.StartAsync(_simCts.Token).FireAndForget(context: "SimulationService.Start");
                }

                // Forward UI WriteCommand / TagUpdates back into MockProtocolDriver for closed-loop control
                var cmdSub = _dataCore.TagUpdates.Subscribe(tag =>
                {
                    if (_isPublishingFromSim || tag.Value == null) return;
                    foreach (var sim in _backgroundSimulators)
                    {
                        if (string.Equals(sim.ConnectionId, tag.ConnId, StringComparison.OrdinalIgnoreCase))
                        {
                            sim.WriteAsync(tag.Address, tag.Value, CancellationToken.None)
                               .FireAndForget(context: "SimulationService.ForwardWrite");
                        }
                    }
                });
                _simSubscriptions.Add(cmdSub);
            }
        }

        public async Task StopSimulationAsync()
        {
            _simCts?.Cancel();
            _simCts?.Dispose();
            _simCts = null;

            _simSubscriptions.Clear();
            
            foreach (var sim in _backgroundSimulators)
            {
                await sim.StopAsync();
                sim.Dispose();
            }
            _backgroundSimulators.Clear();
        }

        public void ResetSimulation()
        {
            foreach (var sim in _backgroundSimulators)
            {
                sim.ResetSimulation();
            }
        }

        public void Dispose()
        {
            _simCts?.Cancel();
            _simSubscriptions.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await StopSimulationAsync();
        }
    }
}
