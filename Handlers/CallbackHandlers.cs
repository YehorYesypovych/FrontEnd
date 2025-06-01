using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Front.Handlers;

public static class CallbackHandlers
{
    public static async Task HandleCallbackQuery(ITelegramBotClient bot, Update update, string apiUrl, 
        CancellationToken cancellationToken)
    {
        var callback = update.CallbackQuery;
        if (callback?.Message == null)
        {
            await bot.AnswerCallbackQuery(callback?.Id ?? "", "❌ Сталася помилка",
                cancellationToken: cancellationToken);
            return;
        }

        var data = callback.Data!;
        var chatId = callback.Message.Chat.Id;

        if (data == "search_by_title")
        {
            UserInputStates.SetSearch(chatId);
            await CommandHandlers.HandleSearchCommand(bot, chatId, apiUrl, cancellationToken);
            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            return;
        }

        if (data == "saved_movies")
        {
            var userId = UserCache.GetUserId(chatId);
            Console.WriteLine($"[{chatId}] used {data}");
            
            if (userId == null)
            {
                await bot.SendMessage(chatId, "⚠️ Користувача не знайдено. Спробуйте спочатку команду /start",
                    cancellationToken: cancellationToken);
                return;
            }
            
            Console.WriteLine($"[{chatId}] GET {apiUrl}/movie/{userId}/saved");
            var json = await HandlerUtils.GetJsonAsync($"{apiUrl}/movie/unwatched/{userId}");

            if (json is null)
            {
                await bot.SendMessage(chatId, "⚠️ Не вдалося отримати збережені фільми",
                    cancellationToken: cancellationToken);
                return;
            }

            var movies = json.Value.EnumerateArray().ToList();
            if (!movies.Any())
            {
                await bot.SendMessage(chatId, "😕 У вас немає відкладених фільмів",
                    cancellationToken: cancellationToken);
                return;
            }

            foreach (var movie in movies)
            {
                var rawDetails = movie.GetProperty("details").GetString();
                var movieId = movie.GetProperty("tmdb_id").GetInt32();
                if (rawDetails is null)
                {
                    Console.WriteLine("❌ details is null");
                    return;
                }

                var movieDetails = JsonDocument.Parse(rawDetails).RootElement;
                var message = HandlerUtils.FormatMovieShort(movieDetails);

                await HandlerUtils.SendMovieWithOptionalPoster(
                    bot,
                    chatId,
                    movieDetails,
                    message,
                    cancellationToken,
                    HandlerUtils.SavedMovieButtons(userId.Value,movieId)
                );
            }

            await bot.SendMessage(
                chatId,
                "Ось ваш список відкладених фільмів",
                replyMarkup: HandlerUtils.GetBackToMenuKeyboard(),
                cancellationToken: cancellationToken);

            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            return;
        }
        if (data == "search_by_genre")
        {
            var userId = UserCache.GetUserId(chatId);
            if (userId == null)
            {
                await bot.SendMessage(chatId, "⚠️ Користувача не знайдено. Спробуйте спочатку команду /start",
                    cancellationToken: cancellationToken);
                return;
            }
            
            var genresJson = await HandlerUtils.GetJsonAsync($"{apiUrl}/app/genres");
            if (genresJson is null)
            {
                await bot.SendMessage(chatId, "❌ Не вдалося отримати список жанрів", cancellationToken: cancellationToken);
                return;
            }

            var buttons = genresJson.Value.EnumerateArray()
                .Where(g => g.TryGetProperty("id", out _) && g.TryGetProperty("name", out _))
                .Select(g =>
                {
                    var id = g.GetProperty("id").GetInt32();
                    var name = g.GetProperty("name").GetString() ?? "Невідомо";
                    return InlineKeyboardButton.WithCallbackData(name, $"search_genre:{id}");
                })
                .Chunk(2)
                .Select(row => row.ToArray())
                .ToArray();

            await bot.SendMessage(
                chatId,
                "🎭 Оберіть жанр:",
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(buttons),
                cancellationToken: cancellationToken);
            
            await bot.SendMessage(chatId,
                "⬇️ Ви можете повернутись назад",
                replyMarkup: HandlerUtils.GetBackToMenuKeyboard(),
                cancellationToken: cancellationToken);

            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            return;
        }
        
        if (data == "watched_filter")
        {
            UserInputStates.SetFilter(chatId);
            await bot.SendMessage(
                chatId,
                "✏️ Введіть мінімальну оцінку (від 1 до 10):",
                replyMarkup: HandlerUtils.GetBackToMenuKeyboard(),
                cancellationToken: cancellationToken);
            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            return;
        }
        
        if (data == "watched_movies")
        {
            var userId = UserCache.GetUserId(chatId);
            if (userId == null)
            {
                await bot.SendMessage(chatId, "⚠️ Користувача не знайдено. Спробуйте спочатку команду /start",
                    cancellationToken: cancellationToken);
                return;
            }

            var json = await HandlerUtils.GetJsonAsync($"{apiUrl}/movie/watched/{userId}");
            if (json is null)
            {
                await bot.SendMessage(chatId, "❌ Не вдалося отримати список переглянутих фільмів", cancellationToken: cancellationToken);
                return;
            }

            var movies = json.Value.EnumerateArray().ToList();
            if (!movies.Any())
            {
                await bot.SendMessage(chatId, "😕 У вас немає переглянутих фільмів", cancellationToken: cancellationToken);
                return;
            }

            foreach (var movie in movies)
            {
                var rawDetails = movie.GetProperty("details").GetString();
                if (string.IsNullOrWhiteSpace(rawDetails)) continue;
 
                var movieDetails = JsonDocument.Parse(rawDetails).RootElement;
                var message = HandlerUtils.FormatMovieShort(movieDetails);
                var movieId = movie.GetProperty("tmdb_id").GetInt32();
                
                
                Console.WriteLine($"{userId}, {movieId}, {userId.GetType()}");
                
                MovieCache.Set(userId.Value, movieId, movieDetails);

                await HandlerUtils.SendMovieWithOptionalPoster(
                    bot,
                    chatId,
                    movieDetails,
                    message,
                    cancellationToken,
                    HandlerUtils.WatchedMovieButtons(userId.Value, movieId)
                );
                
            }
            
            Console.WriteLine("spot_4");
            await bot.SendMessage(
                chatId,
                "⬇️ Я можу відфільтрувати список",
                replyMarkup: HandlerUtils.WatchedFilterButtons(),
                cancellationToken: cancellationToken);

            await bot.SendMessage(chatId,
                "⬇️ Повернення до меню",
                replyMarkup: HandlerUtils.GetBackToMenuKeyboard(),
                cancellationToken: cancellationToken);

            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            return;
        }
        

        
        if (data.StartsWith("search_prev") || data.StartsWith("search_next"))
        {
            var userId = UserCache.GetUserId(chatId);
            if (userId == null) return;

            var current = SearchResultsCache.Get(chatId);
            if (current is null) return;

            var page = current.Value.Page;
            page += data.StartsWith("search_next") ? 1 : -1;
            SearchResultsCache.SetPage(chatId, page);

            await CommandHandlers.ShowSearchPage(bot, chatId, userId.Value, cancellationToken);
            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            return;
        }

        if (data == "back_to_menu")
        {
            await CommandHandlers.HandleStart(bot, chatId, apiUrl, cancellationToken);
            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            return;
        }
        
        if (data.StartsWith("search_genre:") && int.TryParse(data.Split(':')[1], out var parsedGenreId))
        {
            var userId = UserCache.GetUserId(chatId);
            if (userId == null)
            {
                await bot.SendMessage(chatId, "⚠️ Користувача не знайдено. Спробуйте спочатку /start",
                    cancellationToken: cancellationToken);
                await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
                return;
            }

            var json = await HandlerUtils.GetJsonAsync($"{apiUrl}/search-by-genre/{userId}?genre={parsedGenreId}");

            if (json is null)
            {
                await bot.SendMessage(chatId, "⚠️ Не вдалося отримати фільми за жанром",
                    cancellationToken: cancellationToken);
                await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
                return;
            }

            var movies = json.Value.EnumerateArray().ToList();
            if (!movies.Any())
            {
                await bot.SendMessage(chatId, "😕 Немає фільмів у цьому жанрі",
                    cancellationToken: cancellationToken);
                await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
                return;
            }
            
            SearchResultsCache.Set(chatId, movies);
            await CommandHandlers.ShowSearchPage(bot, chatId, userId.Value, cancellationToken);

            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            return;
        }


        

        if (HandlerUtils.TryParseCallbackData(data, "movie_details", out var callbackUserId, out var callbackMovieId))
        {
            if (!MovieCache.TryGet(callbackUserId, callbackMovieId, out var cachedMovie))
            {
                await bot.SendMessage(chatId, "⚠️ Не вдалося знайти фільм у кеші", cancellationToken: cancellationToken);
                return;
            }

            var message = HandlerUtils.FormatMovieFull(cachedMovie);

            var isWatched = cachedMovie.TryGetProperty("watched", out var watchedProp) && watchedProp.GetBoolean();

            var markup = isWatched
                ? HandlerUtils.WatchedMovieFullButtons(callbackUserId, callbackMovieId)
                : HandlerUtils.FullInfoMovieButtons(callbackUserId, callbackMovieId);

            await bot.EditMessageCaption(
                chatId: chatId,
                messageId: callback.Message.MessageId,
                parseMode: ParseMode.Html,
                caption: message,
                replyMarkup: markup,
                cancellationToken: cancellationToken
            );

            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            return;
        }

        
        else if (HandlerUtils.TryParseCallbackData(data, "movie_set_watched", out callbackUserId, out callbackMovieId))
        {
            if (!MovieCache.TryGet(callbackUserId, callbackMovieId, out var movieJson))
            {
                await bot.AnswerCallbackQuery(callback.Id, "⚠️ Фільм не знайдено в кеші", cancellationToken: cancellationToken);
                return;
            }

            var rawJson = movieJson.GetRawText();
            var content = new StringContent(rawJson, Encoding.UTF8, "application/json");

            var response = await HandlerUtils.HttpClient.PostAsync(
                $"{apiUrl}/movie/{callbackUserId}/add-watched", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                await bot.DeleteMessage(chatId, callback.Message.MessageId, cancellationToken);
                await bot.SendMessage(chatId, "✅ Фільм перенесено до списку переглянутих", cancellationToken: cancellationToken);
            }
            else
            {
                await bot.AnswerCallbackQuery(callback.Id, "❌ Не вдалося зберегти фільм як переглянутий", cancellationToken: cancellationToken);
            }
        }

        
        else if (HandlerUtils.TryParseCallbackData(data, "movie_delete", out callbackUserId, out callbackMovieId))
        {
            var deleteUrl = $"{apiUrl}/movie/{callbackUserId}/{callbackMovieId}";
            var response = await HandlerUtils.HttpClient.DeleteAsync(deleteUrl, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                await bot.DeleteMessage(chatId, callback.Message.MessageId, cancellationToken);

                await bot.SendMessage(chatId, "✅ Фільм було видалено зі списку", cancellationToken: cancellationToken);
            }
            else
            {
                await bot.AnswerCallbackQuery(callback.Id, "❌ Не вдалося видалити фільм", cancellationToken: cancellationToken);
            }
        }
        
        else if (HandlerUtils.TryParseCallbackData(data, "movie_rate", out callbackUserId, out callbackMovieId))
        {
            await bot.SendMessage(chatId,
                "📝 Введіть вашу оцінку від 1 до 10 для цього фільму:",
                replyMarkup: HandlerUtils.GetBackToMenuKeyboard(),
                cancellationToken: cancellationToken);

            UserInputStates.SetRating(chatId);

            MessageCache.Set(chatId, callback.Message.MessageId);

            var tempData = new
            {
                userId = callbackUserId,
                movieId = callbackMovieId
            };

            var json = JsonSerializer.SerializeToElement(tempData);
            SearchResultsCache.Set(chatId, new List<JsonElement> { json });

            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            return;
        }






        


        else if (HandlerUtils.TryParseCallbackData(data, "movie_save", out callbackUserId, out callbackMovieId))
        {
            if (!MovieCache.TryGet(callbackUserId, callbackMovieId, out var movieJson))
            {
                await bot.SendMessage(chatId, "⚠️ Не вдалося знайти фільм у кеші",
                    cancellationToken: cancellationToken);
                return;
            }

            var rawJson = movieJson.GetRawText();

            var jsonContent = $"{rawJson}";

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var response =
                await HandlerUtils.HttpClient.PostAsync($"{apiUrl}/movie/{callbackUserId}/save", content, cancellationToken);

            var resultMsg = response.IsSuccessStatusCode ? "✅ Збережено до списку" : "❌ Не вдалося зберегти";
            await bot.SendMessage(chatId, resultMsg, cancellationToken: cancellationToken);
        }
        else if (HandlerUtils.TryParseCallbackData(data, "movie_collapse", out callbackUserId, out callbackMovieId))
        {
            if (!MovieCache.TryGet(callbackUserId, callbackMovieId, out var movieJson))
            {
                await bot.SendMessage(chatId, "⚠️ Не вдалося знайти фільм у кеші",
                    cancellationToken: cancellationToken);
                return;
            }

            var message = HandlerUtils.FormatMovieShort(movieJson);

            await bot.EditMessageCaption(
                chatId: chatId,
                messageId: callback.Message.MessageId,
                parseMode: ParseMode.Html,
                caption: message,
                replyMarkup: HandlerUtils.CreateMovieButtons(callbackUserId, callbackMovieId),
                cancellationToken: cancellationToken
            );
        }
        else if (HandlerUtils.TryParseCallbackData(data, "set_watched", out callbackUserId, out callbackMovieId))
        {
            if (!MovieCache.TryGet(callbackUserId, callbackMovieId, out var movieJson))
            {
                await bot.SendMessage(chatId, "⚠️ Не вдалося знайти фільм у кеші",
                    cancellationToken: cancellationToken);
                return;
            }

            var rawJson = movieJson.GetRawText();
            var content = new StringContent(rawJson, Encoding.UTF8, "application/json");

            var response = await HandlerUtils.HttpClient.PostAsync(
                $"{apiUrl}/movie/{callbackUserId}/add-watched", content, cancellationToken);

            var msg = response.IsSuccessStatusCode
                ? "✅ Фільм додано як переглянутий"
                : "❌ Не вдалося зберегти фільм як переглянутий";

            await bot.SendMessage(chatId, msg, cancellationToken: cancellationToken);
        }
        if (HandlerUtils.TryParseCallbackData(data, "mds", out callbackUserId, out callbackMovieId))
{
    var json = await HandlerUtils.GetJsonAsync($"{apiUrl}/movie/{callbackMovieId}");
    if (json is null) return;

    var message = HandlerUtils.FormatMovieFull(json.Value);
    await bot.EditMessageCaption(
        chatId: chatId,
        messageId: callback.Message.MessageId,
        caption: message,
        parseMode: ParseMode.Html,
        replyMarkup: HandlerUtils.SavedMovieFullButtons(callbackUserId, callbackMovieId),
        cancellationToken: cancellationToken
    );
    return;
}
if (HandlerUtils.TryParseCallbackData(data, "mcs", out callbackUserId, out callbackMovieId))
{
    if (!MovieCache.TryGet(callbackUserId, callbackMovieId, out var movieJson)) return;

    var message = HandlerUtils.FormatMovieShort(movieJson);
    await bot.EditMessageCaption(
        chatId: chatId,
        messageId: callback.Message.MessageId,
        caption: message,
        parseMode: ParseMode.Html,
        replyMarkup: HandlerUtils.SavedMovieButtons(callbackUserId, callbackMovieId),
        cancellationToken: cancellationToken
    );
    return;
}

if (HandlerUtils.TryParseCallbackData(data, "mdw", out callbackUserId, out callbackMovieId))
{
    if (!MovieCache.TryGet(callbackUserId, callbackMovieId, out var cachedMovie))
    {
        await bot.SendMessage(chatId, "⚠️ Не вдалося знайти фільм у кеші", cancellationToken: cancellationToken);
        return;
    }

    var message = HandlerUtils.FormatMovieFull(cachedMovie);

    await bot.EditMessageCaption(
        chatId: chatId,
        messageId: callback.Message.MessageId,
        caption: message,
        parseMode: ParseMode.Html,
        replyMarkup: HandlerUtils.WatchedMovieFullButtons(callbackUserId, callbackMovieId),
        cancellationToken: cancellationToken
    );


    await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
    return;
}

if (HandlerUtils.TryParseCallbackData(data, "mcw", out callbackUserId, out callbackMovieId))
{
    if (!MovieCache.TryGet(callbackUserId, callbackMovieId, out var movieJson)) return;

    var message = HandlerUtils.FormatMovieShort(movieJson);
    await bot.EditMessageCaption(
        chatId: chatId,
        messageId: callback.Message.MessageId,
        caption: message,
        parseMode: ParseMode.Html,
        replyMarkup: HandlerUtils.WatchedMovieButtons(callbackUserId, callbackMovieId),
        cancellationToken: cancellationToken
    );
    return;
}





        await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
    }
}