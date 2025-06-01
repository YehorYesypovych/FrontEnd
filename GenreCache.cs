namespace Front;

public static class GenreCache
{
    private static readonly Dictionary<int, string> _genres = new();

    public static void SetGenres(Dictionary<int, string> genres)
    {
        _genres.Clear();
        foreach (var pair in genres)
            _genres[pair.Key] = pair.Value;
    }

    public static string? GetName(int id) =>
        _genres.TryGetValue(id, out var name) ? name : null;

    public static bool IsLoaded => _genres.Any();
}