namespace Front;

public static class MessageCache
{
    private static readonly Dictionary<long, int> Cache = new();

    public static void Set(long chatId, int messageId)
    {
        Cache[chatId] = messageId;
    }

    public static int? Get(long chatId)
    {
        return Cache.TryGetValue(chatId, out var id) ? id : null;
    }

    public static void Clear(long chatId)
    {
        Cache.Remove(chatId);
    }
}