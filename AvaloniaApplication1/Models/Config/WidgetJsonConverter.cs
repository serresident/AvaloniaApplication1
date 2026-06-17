using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AvaloniaApplication1.Models.Config
{
    public class WidgetJsonConverter : JsonConverter<WidgetConfig>
    {
        public override WidgetConfig? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            using var doc = JsonDocument.ParseValue(ref reader);
            if (!doc.RootElement.TryGetProperty("Type", out var typeProperty))
                throw new JsonException("Widget type is missing");

            var typeName = typeProperty.GetString();

            // Deserializing without the converter to avoid infinite recursion
            var deserializeOptions = new JsonSerializerOptions(options);
            // We can't easily remove this specific converter from options in System.Text.Json,
            // but since we're deserializing into derived classes that DON'T have the [JsonConverter] attribute on them,
            // it won't infinite-loop. The base class has the attribute.

            WidgetConfig? config = typeName switch
            {
                "ValueDisplay" => JsonSerializer.Deserialize<ValueDisplayConfig>(doc.RootElement.GetRawText(), deserializeOptions),
                "PilotLight" => JsonSerializer.Deserialize<PilotLightConfig>(doc.RootElement.GetRawText(), deserializeOptions),
                "ContainerButton" => JsonSerializer.Deserialize<ContainerButtonConfig>(doc.RootElement.GetRawText(), deserializeOptions),
                "CommandButton" => JsonSerializer.Deserialize<CommandButtonConfig>(doc.RootElement.GetRawText(), deserializeOptions),
                "Slider" => JsonSerializer.Deserialize<SliderConfig>(doc.RootElement.GetRawText(), deserializeOptions),
                "SetValue" => JsonSerializer.Deserialize<SetValueConfig>(doc.RootElement.GetRawText(), deserializeOptions),
                "RealTimeTrend" => JsonSerializer.Deserialize<RealTimeTrendConfig>(doc.RootElement.GetRawText(), deserializeOptions),
                "Pipe" => JsonSerializer.Deserialize<PipeConfig>(doc.RootElement.GetRawText(), deserializeOptions),
                "Valve" => JsonSerializer.Deserialize<ValveConfig>(doc.RootElement.GetRawText(), deserializeOptions),
                "Tank" => JsonSerializer.Deserialize<TankConfig>(doc.RootElement.GetRawText(), deserializeOptions),
                "Pump" => JsonSerializer.Deserialize<PumpConfig>(doc.RootElement.GetRawText(), deserializeOptions),
                _ => JsonSerializer.Deserialize<WidgetConfigBase>(doc.RootElement.GetRawText(), deserializeOptions) // Fallback
            };

            return config;
        }

        public override void Write(Utf8JsonWriter writer, WidgetConfig value, JsonSerializerOptions options)
        {
            // Serialize as the actual runtime type
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}
