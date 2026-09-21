using System.Threading.Tasks;
using AvaloniaApplication1.Models.Config;

namespace AvaloniaApplication1.Services
{
    public interface IConfigurationService
    {
        string CurrentFilePath { get; set; }
        Task<HmiConfiguration> LoadConfigurationAsync(string? filePath = null);
        Task SaveConfigurationAsync(HmiConfiguration config, string? filePath = null);
    }
}