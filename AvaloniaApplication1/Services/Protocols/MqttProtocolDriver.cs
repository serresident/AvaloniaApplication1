using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Models.Config;
using MQTTnet;
using MQTTnet.Client;

namespace AvaloniaApplication1.Services.Protocols
{
    public class MqttProtocolDriver : IProtocolDriver
    {
        public string ConnectionId { get; }
        private readonly ConnectionConfig _config;
        private readonly IEnumerable<string> _topicsToSubscribe;
        private readonly Subject<TagData> _tagUpdates = new();
        private IMqttClient? _mqttClient;

        public IObservable<TagData> TagUpdates => _tagUpdates;

        public MqttProtocolDriver(ConnectionConfig config, IEnumerable<string> topicsToSubscribe)
        {
            _config = config;
            ConnectionId = config.Id;
            _topicsToSubscribe = topicsToSubscribe.Distinct().ToList();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_config.Host, _config.Port)
                .WithCleanSession()
                .Build();

            _mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                string topic = e.ApplicationMessage.Topic;
                string payload = System.Text.Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

                // Note: We used to parse data types here based on widget config. 
                // For a generic driver, we can parse as double/bool or just emit as string 
                // and let the view model handle it. For now, doing a basic heuristic or returning float/bool.
                
                object parsedVal = payload;
                if (payload == "1" || payload.Equals("true", StringComparison.OrdinalIgnoreCase))
                    parsedVal = true;
                else if (payload == "0" || payload.Equals("false", StringComparison.OrdinalIgnoreCase))
                    parsedVal = false;
                else if (float.TryParse(payload, NumberStyles.Float, CultureInfo.InvariantCulture, out float fVal)) 
                    parsedVal = fVal;

                _tagUpdates.OnNext(new TagData(ConnectionId, topic, parsedVal));
                return Task.CompletedTask;
            };

            // Reconnection loop
            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (!_mqttClient.IsConnected)
                        {
                            Console.WriteLine($"MQTT connecting to {_config.Host}:{_config.Port}...");
                            await _mqttClient.ConnectAsync(options, cancellationToken);
                            Console.WriteLine("MQTT connected!");

                            foreach (var topic in _topicsToSubscribe)
                            {
                                await _mqttClient.SubscribeAsync(topic);
                                Console.WriteLine($"MQTT Subscribed to: {topic}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"MQTT connection failed: {ex.Message}");
                    }

                    await Task.Delay(5000, cancellationToken);
                }
            }, cancellationToken).FireAndForget(context: "MqttProtocolDriver");
        }

        public async Task WriteAsync(string address, object value, CancellationToken cancellationToken)
        {
            if (_mqttClient == null || !_mqttClient.IsConnected) return;

            string payload = value.ToString() ?? "";
            if (value is bool b)
            {
                payload = b ? "true" : "false";
            }

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(address)
                .WithPayload(payload)
                .WithRetainFlag(true)
                .Build();

            await _mqttClient.PublishAsync(message, cancellationToken);
        }

        public async Task StopAsync()
        {
            if (_mqttClient != null)
            {
                await _mqttClient.DisconnectAsync();
                _mqttClient.Dispose();
                _mqttClient = null;
            }
        }

        public void Dispose()
        {
            _tagUpdates.Dispose();
            _mqttClient?.Dispose();
        }
    }
}
