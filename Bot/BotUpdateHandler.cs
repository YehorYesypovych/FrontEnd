using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Front.Config;
using Front.Handlers;

namespace Front.Bot;

public static class BotUpdateHandler
{
    public static async Task HandleUpdate(ITelegramBotClient bot, Update update, AppSettings settings, CancellationToken cancellationToken)
    {
        var apiUrl = settings.Backend.ApiUrl;

        if (update.Type == UpdateType.CallbackQuery)
        {
            await CallbackHandlers.HandleCallbackQuery(bot, update, apiUrl, cancellationToken);
            return;
        }
        

        if (update.Message is { Text: { } messageText })
        {
            var chatId = update.Message.Chat.Id;
            
            if (messageText == "⬅️ Повернутись в головне меню")
            {
                UserInputStates.Clear(chatId);
                SearchResultsCache.Clear(chatId);
                await CommandHandlers.HandleStart(bot, chatId, apiUrl, cancellationToken, isReturning: true);
                return;
            }


            if (messageText == "⬅️ Попередня сторінка" || messageText == "➡️ Наступна сторінка")
            {
                var userId = UserCache.GetUserId(chatId);
                if (userId == null) return;

                var data = SearchResultsCache.Get(chatId);
                if (data is null) return;

                var currentPage = data.Value.Page;
                if (messageText == "⬅️ Попередня сторінка") currentPage--;
                else if (messageText == "➡️ Наступна сторінка") currentPage++;

                SearchResultsCache.SetPage(chatId, currentPage);
                await CommandHandlers.ShowSearchPage(bot, chatId, userId.Value, cancellationToken);
                return;
            }
            if (messageText == "🔁 Показати всі переглянуті")
            {
                var userId = UserCache.GetUserId(chatId);
                if (userId != null)
                {
                    var upd = new Update { CallbackQuery = new CallbackQuery { Message = new Message { Chat = new Chat { Id = chatId } }, Data = "watched_movies" } };
                    await CallbackHandlers.HandleCallbackQuery(bot, upd, apiUrl, cancellationToken);
                }
                return;
            }




            if (await HandleCommand(bot, messageText, chatId, apiUrl, cancellationToken))
                return;

            if (UserInputStates.IsSearch(chatId))
            {
                
                UserInputStates.Clear(chatId);
                await CommandHandlers.HandleTextInput(bot, chatId, messageText, apiUrl, cancellationToken);
                return;
            }

            if (UserInputStates.IsRating(chatId))
            {
                await CommandHandlers.HandleRatingInput(bot, chatId, messageText, apiUrl, cancellationToken);
                return;
            }
            if (UserInputStates.IsFilter(chatId))
            {
                if (decimal.TryParse(messageText.Replace(',', '.'), out var minRating) &&
                    minRating >= 1 && minRating <= 10)
                {

                    await CommandHandlers.ShowFilteredWatched(bot, chatId, apiUrl, minRating, cancellationToken);
                }

                else
                {
                    await bot.SendMessage(
                        chatId,
                        "⚠️ Введіть число від 1 до 10",
                        replyMarkup: HandlerUtils.GetBackToMenuKeyboard(),
                        cancellationToken: cancellationToken
                    );
                }
                return;
            }



        }
    }

    private static async Task<bool> HandleCommand(ITelegramBotClient bot, string messageText, long chatId, string apiUrl, CancellationToken cancellationToken)
    {
        switch (messageText)
        {
            case "/start":
                Console.WriteLine($"[{chatId}] used /start command");
                await CommandHandlers.HandleStart(bot, chatId, apiUrl, cancellationToken);
                return true;

            case "/random":
                Console.WriteLine($"[{chatId}] used /random command");
                await CommandHandlers.HandleRandom(bot, chatId, apiUrl, cancellationToken);
                return true;

            case "/search":
                Console.WriteLine($"[{chatId}] used /search command");
                UserInputStates.SetSearch(chatId);
                await CommandHandlers.HandleSearchCommand(bot, chatId, apiUrl, cancellationToken: cancellationToken);
                return true;
            case "/top":
                Console.WriteLine($"[{chatId}] used /top command");
                await CommandHandlers.HandleTopSaved(bot, chatId, apiUrl, cancellationToken);
                return true;
            case "/stats":
                Console.WriteLine($"[{chatId}] used /stats command");
                await CommandHandlers.HandleStats(bot, chatId, apiUrl, cancellationToken);
                return true;
            case string cmd when cmd.StartsWith("/filter"):
                var parts = cmd.Split(' ');
                if (parts.Length == 2 && decimal.TryParse(parts[1], out var minRating))
                {
                    Console.WriteLine($"[{chatId}] used /filter {minRating}");
                    await CommandHandlers.HandleFilteredWatched(bot, chatId, apiUrl, minRating, cancellationToken);
                    return true;
                }
                await bot.SendMessage(chatId, "⚠️ Приклад використання: /filter 7", cancellationToken: cancellationToken);
                return true;



            default:
                return false;
        }
    }
}