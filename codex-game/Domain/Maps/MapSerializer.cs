using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodexGame.Domain.Maps;

public static class MapSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string ToJson(MapData data) => JsonSerializer.Serialize(data, Options);

    public static MapData FromJson(string json) => JsonSerializer.Deserialize<MapData>(json, Options)!;
}
