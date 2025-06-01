using Telegram.Bot;


namespace Front.Services;

public class ReminderService
{
    private readonly TelegramBotClient _bot;
    private readonly string _message;
    private readonly TimeSpan _interval;
    private readonly CancellationToken _cancellationToken;

    public ReminderService(TelegramBotClient bot, string message, TimeSpan interval, CancellationToken cancellationToken)
    {
        _bot = bot;
        _message = message;
        _interval = interval;
        _cancellationToken = cancellationToken;
    }

    public async Task StartAsync()
    {
        while (!_cancellationToken.IsCancellationRequested)
        {
            foreach (var chatId in UserCache.GetAllChatIds())
            {
                try
                {
                    await _bot.SendMessage(chatId, _message, cancellationToken: _cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Помилка надсилання нагадування до chatId={chatId}: {ex.Message}");
                }
            }

            await Task.Delay(_interval, _cancellationToken);
        }
    }
}