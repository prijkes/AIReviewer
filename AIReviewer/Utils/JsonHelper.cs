using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIReviewer.Reviewer.Utils;

public static class JsonHelpers
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowTrailingCommas = true
    };

    public static T DeserializeStrict<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidOperationException("Deserialized null JSON");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"JSON parse failed at {ex.Path}: {ex.Message}", ex);
        }
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}
