using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameEngine;

public static class GameStateSerializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string payload) =>
        JsonSerializer.Deserialize<T>(payload, Options)
        ?? throw new InvalidDataException($"Saved {typeof(T).Name} is empty.");

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
