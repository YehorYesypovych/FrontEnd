namespace Front.Config;

public class AppSettings
{
    public TelegramConfig Telegram { get; set; } = null!;
    public BackendConfig Backend { get; set; } = null!;
}

public class TelegramConfig
{
    public string Token { get; set; } = string.Empty;
    public long AdminChatId { get; set; }
}

public class BackendConfig
{
    public string ApiUrl { get; set; } = string.Empty;
}