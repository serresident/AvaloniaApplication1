using System;
using System.Collections.Generic;
using AvaloniaApplication1.Models.Config;

namespace AvaloniaApplication1.Services.Protocols
{
    public class ProtocolDriverFactory : IProtocolDriverFactory
    {
        private readonly Dictionary<string, Func<ConnectionConfig, IEnumerable<DataSourceConfig>, IProtocolDriver>> _creators = new(StringComparer.OrdinalIgnoreCase);

        public void RegisterDriver(string connectionType, Func<ConnectionConfig, IEnumerable<DataSourceConfig>, IProtocolDriver> creator)
        {
            _creators[connectionType] = creator;
        }

        public IProtocolDriver? CreateDriver(ConnectionConfig config, IEnumerable<DataSourceConfig> tags)
        {
            if (_creators.TryGetValue(config.Type, out var creator))
            {
                return creator(config, tags);
            }

            // Fallback for known aliases like ModbusRTUOverTCP mapped to ModbusTCP in our basic implementation
            if (config.Type == "ModbusRTUOverTCP" && _creators.TryGetValue("ModbusTCP", out var rtuCreator))
            {
                return rtuCreator(config, tags);
            }

            return null;
        }
    }
}
