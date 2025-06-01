﻿using System.Text.Json;
using Front;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Front.Config;
using Front.Bot;
using Front.Handlers;
using Front.Services;

var settings = LoadSettings();
var botClient = new TelegramBotClient(settings.Telegram.Token);
var cts = new CancellationTokenSource();

await StartBot(botClient, settings, cts.Token);
await LoadGenres(settings.Backend.ApiUrl);
Console.ReadLine();


AppSettings LoadSettings()
{
    var configJson = File.ReadAllText("appsettings.json");
    return JsonSerializer.Deserialize<AppSettings>(configJson) ?? throw new Exception("❌ Не вдалося завантажити конфігурацію.");
}

async Task StartBot(TelegramBotClient client, AppSettings appSettings, CancellationToken cancellationToken)
{
    await client.DeleteWebhook(cancellationToken: cancellationToken);

    client.StartReceiving(
        (bot, update, token) => BotUpdateHandler.HandleUpdate(bot, update, appSettings, token),
        HandleError,
        new ReceiverOptions { AllowedUpdates = [] },
        cancellationToken: cancellationToken
    );

    await client.SendMessage(appSettings.Telegram.AdminChatId, "✅ Бот запущено та готовий до роботи, для початку використовуйте команду /start", cancellationToken: cancellationToken);
    Console.WriteLine("✅ Bot is running...");
    
    var reminder = new Front.Services.ReminderService(
        botClient,
        "🔔 Нагадування: перевір свої збережені фільми!",
        TimeSpan.FromHours(10),
        cts.Token
    );

    _ = Task.Run(() => reminder.StartAsync());
    
}
async Task LoadGenres(string apiUrl)
{
    var response = await HandlerUtils.HttpClient.GetAsync($"{apiUrl}/app/genres");
    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine("❌ Не вдалося завантажити жанри");
        return;
    }

    var json = await response.Content.ReadAsStringAsync();
    var parsed = JsonDocument.Parse(json).RootElement;

    var genres = parsed.EnumerateArray()
        .Where(g => g.TryGetProperty("id", out _) && g.TryGetProperty("name", out _))
        .ToDictionary(
            g => g.GetProperty("id").GetInt32(),
            g => g.GetProperty("name").GetString() ?? "Невідомо");

    GenreCache.SetGenres(genres);
    Console.WriteLine($"✅ Завантажено {genres.Count} жанрів");
}

Task HandleError(ITelegramBotClient bot, Exception exception, CancellationToken cancellationToken)
{
    Console.WriteLine($"Error: {exception.Message}");
    return Task.CompletedTask;
}