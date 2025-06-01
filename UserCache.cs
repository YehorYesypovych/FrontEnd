namespace Front;

public static class UserCache
{
    private static readonly Dictionary<long, Guid> ChatToUserMap = new();

    public static void SetUserId(long chatId, Guid userId)
        => ChatToUserMap[chatId] = userId;

    public static Guid? GetUserId(long chatId)
        => ChatToUserMap.TryGetValue(chatId, out var userId) ? userId : null;
    
    public static IEnumerable<long> GetAllChatIds() => ChatToUserMap.Keys;
}