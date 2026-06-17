using System.Collections.Generic;
using AvaloniaApplication1.Models.Config;

namespace AvaloniaApplication1.Services.Protocols
{
    public interface IProtocolDriverFactory
    {
        IProtocolDriver? CreateDriver(ConnectionConfig config, IEnumerable<DataSourceConfig> tags);
    }
}
