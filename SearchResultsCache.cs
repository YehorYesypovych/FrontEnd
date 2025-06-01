using System.Text.Json;

namespace Front;

public static class SearchResultsCache
{
    private static readonly Dictionary<long, (List<JsonElement> Movies, int Page)> _cache = new();

    public static void Set(long chatId, List<JsonElement> movies)
    {
        _cache[chatId] = (movies, 0);
    }

    public static (List<JsonElement> Movies, int Page)? Get(long chatId)
    {
        return _cache.TryGetValue(chatId, out var value) ? value : null;
    }

    public static void SetPage(long chatId, int page)
    {
        if (_cache.TryGetValue(chatId, out var data))
        {
            _cache[chatId] = (data.Movies, page);
        }
    }

    public static void Clear(long chatId)
    {
        _cache.Remove(chatId);
    }
}