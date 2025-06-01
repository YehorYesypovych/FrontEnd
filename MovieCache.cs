using System.Text.Json;

namespace Front;

public static class MovieCache
{
    private static readonly Dictionary<string, JsonElement> Cache = new();

    public static void Set(Guid userId, int movieId, JsonElement json)
    {
        var key = $"{userId}:{movieId}";
        Cache[key] = json;
    }

    public static bool TryGet(Guid userId, int movieId, out JsonElement json)
    {
        var key = $"{userId}:{movieId}";
        return Cache.TryGetValue(key, out json);
    }
    
    public static void UpdateUserRating(Guid userId, int movieId, double rating)
    {
        var key = $"{userId}:{movieId}";
        if (!Cache.TryGetValue(key, out var existing)) return;

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();

        foreach (var prop in existing.EnumerateObject())
        {
            if (prop.NameEquals("user_rating")) continue;
            prop.WriteTo(writer);
        }

        writer.WriteNumber("user_rating", rating);

        writer.WriteEndObject();
        writer.Flush();

        var updated = JsonDocument.Parse(stream.ToArray()).RootElement;
        Cache[key] = updated;
    }

}