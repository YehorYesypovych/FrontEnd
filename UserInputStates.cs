namespace Front;

public static class UserInputStates
{
    private enum State
    {
        None,
        Search,
        Rating,
        Filter
    }

    private static readonly Dictionary<long, State> _states = new();

    public static void SetSearch(long chatId) => _states[chatId] = State.Search;
    public static void SetRating(long chatId) => _states[chatId] = State.Rating;
    public static void SetFilter(long chatId) => _states[chatId] = State.Filter;

    public static bool IsSearch(long chatId) => _states.TryGetValue(chatId, out var state) && state == State.Search;
    public static bool IsRating(long chatId) => _states.TryGetValue(chatId, out var state) && state == State.Rating;
    public static bool IsFilter(long chatId) => _states.TryGetValue(chatId, out var state) && state == State.Filter;

    public static void Clear(long chatId) => _states.Remove(chatId);
}