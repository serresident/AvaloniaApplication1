using System;
using System.Threading.Tasks;

namespace AvaloniaApplication1.Services
{
    public interface ISimulationService : IAsyncDisposable
    {
        Task StartSimulationAsync();
        Task StopSimulationAsync();
        void ResetSimulation();
    }
}
