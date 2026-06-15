using System.Threading.Tasks;
using AvaloniaApplication1.Models.Config;

namespace AvaloniaApplication1.Services
{
    public interface IConfigurationService
    {
        Task<HmiConfiguration> LoadConfigurationAsync();
        Task SaveConfigurationAsync(HmiConfiguration config);
    }
}