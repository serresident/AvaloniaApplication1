using System.Threading.Tasks;

namespace AvaloniaApplication1.Services
{
    public interface ISimulationService
    {
        Task StartSimulationAsync();
        Task StopSimulationAsync();
        void ResetSimulation();
    }
}
