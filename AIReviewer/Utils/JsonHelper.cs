using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIReviewer.Utils;

/// <summary>
/// Utility class for JSON serialization and deserialization operations.
/// Provides strict deserialization and consistent formatting options.
/// </summary>
public static class JsonHelpers
{
    /// <summary>
    /// JSON serializer options configured for camelCase naming and strict parsing.
    /// </summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Deserializes JSON with strict validation, throwing detailed exceptions on parse errors.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized object.</returns>
    /// <exception cref="InvalidOperationException">Thrown when deserialization results in null.</exception>
    /// <exception cref="InvalidDataException">Thrown when JSON parsing fails.</exception>
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

    /// <summary>
    /// Serializes an object to JSON using the configured options.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <returns>A JSON string representation of the object.</returns>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}
