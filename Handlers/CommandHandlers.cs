using System.Net.Http.Json;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace Front.Handlers;

public static class CommandHandlers
{
    private static async Task TryFetchUserId(ITelegramBotClient bot, long chatId, string apiUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var response =
                await HandlerUtils.HttpClient.PostAsJsonAsync($"{apiUrl}/user/save", new { chatId }, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var root = JsonDocument.Parse(json).RootElement;

                if (root.TryGetProperty("id", out var idProp) && Guid.TryParse(idProp.GetString(), out var userId))
                {
                    UserCache.SetUserId(chatId, userId);
                    Console.WriteLine($"✅ Користувача додано до локального кешу: {userId}");
                }
            }
            else
            {
                Console.WriteLine($"❌ Не вдалося зберегти користувача. Код: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Помилка при отриманні userId: {ex.Message}");
        }
    }
    

    public static async Task HandleStart(ITelegramBotClient bot, long chatId, string apiUrl,
        CancellationToken cancellationToken, bool isReturning = false)
    {
        var greeting = isReturning
            ? "📍 Ви знаходитесь в головному меню"
            : "🎬 Вітаю! Я бот для пошуку фільмів через TMDB.";

        await TryFetchUserId(bot, chatId, apiUrl, cancellationToken);

        var inlineKeyboard = new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData("🔍 Пошук за назвою", "search_by_title"),
                InlineKeyboardButton.WithCallbackData("🎭 Пошук за жанром", "search_by_genre")
            ],
            [
                InlineKeyboardButton.WithCallbackData("✅ Переглянуті фільми", "watched_movies"),
                InlineKeyboardButton.WithCallbackData("🕒 Відкладені фільми", "saved_movies")
            ]
        ]);

        await bot.SendMessage(
            chatId,
            greeting,
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken);

        await bot.SendMessage(
            chatId,
            "Оберіть дію нижче:",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);
    }


    public static async Task HandleRandom(ITelegramBotClient bot, long chatId, string apiUrl,
        CancellationToken cancellationToken)
    {
        var userId = UserCache.GetUserId(chatId);
        if (userId == null)
        {
            Console.WriteLine($"ℹ️ userId відсутній, виконуємо авто-створення для chatId={chatId}");
            await TryFetchUserId(bot, chatId, apiUrl, cancellationToken);
            userId = UserCache.GetUserId(chatId);

            if (userId == null)
            {
                await bot.SendMessage(chatId, "❗ Не вдалося створити користувача. Спробуйте ще раз пізніше.",
                    cancellationToken: cancellationToken);
                return;
            }
        }

        var json = await HandlerUtils.GetJsonAsync($"{apiUrl}/movie/random");
        if (json is null)
        {
            await bot.SendMessage(chatId, "⚠️ Не вдалося отримати фільм", cancellationToken: cancellationToken);
            return;
        }

        var message = HandlerUtils.FormatMovieShort(json.Value);
        var movieId = json.Value.GetProperty("id").GetInt32();
        var keyboard = HandlerUtils.CreateMovieButtons(userId.Value, movieId);

        MovieCache.Set(userId.Value, movieId, json.Value);

        await HandlerUtils.SendMovieWithOptionalPoster(
            bot,
            chatId,
            json.Value,
            message,
            cancellationToken,
            keyboard);

        await bot.SendMessage(
            chatId,
            "⬇️ Оберіть дію або поверніться до меню",
            replyMarkup: HandlerUtils.GetBackToMenuKeyboard(),
            cancellationToken: cancellationToken);
    }



    public static async Task HandleSearchCommand(ITelegramBotClient bot, long chatId, string apiUrl,
        CancellationToken cancellationToken)
    {
        var userId = UserCache.GetUserId(chatId);
        if (userId == null)
        {
            Console.WriteLine($"ℹ️ userId відсутній, виконуємо /start автоматично для chatId={chatId}");
            await TryFetchUserId(bot, chatId, apiUrl, cancellationToken);
        }

        await bot.SendMessage(
            chatId,
            "✏️ Введіть назву фільму для пошуку:",
            cancellationToken: cancellationToken,
            replyMarkup: HandlerUtils.GetBackToMenuKeyboard());
    }
    
    public static async Task HandleTopSaved(ITelegramBotClient bot, long chatId, string apiUrl, CancellationToken cancellationToken)
{
    var userId = UserCache.GetUserId(chatId);
    if (userId == null)
    {
        await bot.SendMessage(chatId, "⚠️ Користувача не знайдено. Спробуйте спочатку команду /start", cancellationToken: cancellationToken);
        return;
    }

    var json = await HandlerUtils.GetJsonAsync($"{apiUrl}/movie/unwatched/{userId}");
    if (json is null)
    {
        await bot.SendMessage(chatId, "⚠️ Не вдалося отримати список відкладених фільмів", cancellationToken: cancellationToken);
        return;
    }

    var movies = json.Value.EnumerateArray()
        .Select(movie =>
        {
            var rawDetails = movie.GetProperty("details").GetString();
            var movieId = movie.GetProperty("tmdb_id").GetInt32();
            if (rawDetails is null) return null;

            var details = JsonDocument.Parse(rawDetails).RootElement;
            var rating = details.TryGetProperty("vote_average", out var r) && r.TryGetDecimal(out var rate) ? rate : 0;
            return new { Movie = details, Rating = rating, MovieId = movieId };
        })
        .Where(x => x != null)
        .OrderByDescending(x => x!.Rating)
        .Take(5)
        .ToList();

    if (!movies.Any())
    {
        await bot.SendMessage(chatId, "😕 У вас немає збережених фільмів для показу топу", cancellationToken: cancellationToken);
        return;
    }

    foreach (var item in movies)
    {
        MovieCache.Set(userId.Value, item!.MovieId, item.Movie);
        var message = HandlerUtils.FormatMovieShort(item.Movie);
        await HandlerUtils.SendMovieWithOptionalPoster(
            bot, chatId, item.Movie, message, cancellationToken, HandlerUtils.SavedMovieButtons(userId.Value, item.MovieId));
    }

    await bot.SendMessage(chatId,
        "⬇️ Повернення до меню",
        replyMarkup: HandlerUtils.GetBackToMenuKeyboard(),
        cancellationToken: cancellationToken);
}
    public static async Task HandleStats(ITelegramBotClient bot, long chatId, string apiUrl, CancellationToken cancellationToken)
    {
        var userId = UserCache.GetUserId(chatId);
        if (userId == null)
        {
            await bot.SendMessage(chatId, "⚠️ Користувача не знайдено. Спробуйте спочатку /start", cancellationToken: cancellationToken);
            return;
        }

        var statsJson = await HandlerUtils.GetJsonAsync($"{apiUrl}/movie/stats/{userId}");
        if (statsJson is null)
        {
            await bot.SendMessage(chatId, "❌ Не вдалося отримати статистику", cancellationToken: cancellationToken);
            return;
        }

        var watched = statsJson.Value.GetProperty("watched").GetInt32();
        var unwatched = statsJson.Value.GetProperty("unwatched").GetInt32();

        var msg = $"📊 <b>Ваша статистика</b>:\n" +
                  $"✅ Переглянуто: <b>{watched}</b>\n" +
                  $"💾 Відкладено: <b>{unwatched}</b>";

        await bot.SendMessage(chatId, msg, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: HandlerUtils.GetBackToMenuKeyboard(),
            cancellationToken: cancellationToken);
    }
    public static async Task HandleFilteredWatched(ITelegramBotClient bot, long chatId, string apiUrl, decimal minRating, CancellationToken cancellationToken)
    {
        var userId = UserCache.GetUserId(chatId);
        if (userId == null)
        {
            await bot.SendMessage(chatId, "⚠️ Користувача не знайдено. Спробуйте /start", cancellationToken: cancellationToken);
            return;
        }

        var json = await HandlerUtils.GetJsonAsync($"{apiUrl}/movie/watched/{userId}/filter?minRating={minRating}");
        if (json is null)
        {
            await bot.SendMessage(chatId, "❌ Не вдалося отримати фільми", cancellationToken: cancellationToken);
            return;
        }

        var movies = json.Value.EnumerateArray().ToList();
        if (!movies.Any())
        {
            await bot.SendMessage(chatId, $"😕 У вас немає фільмів з оцінкою більше {minRating}", cancellationToken: cancellationToken);
            return;
        }

        foreach (var movie in movies)
        {
            var rawDetails = movie.GetProperty("details").GetString();
            if (string.IsNullOrWhiteSpace(rawDetails)) continue;

            var movieDetails = JsonDocument.Parse(rawDetails).RootElement;
            var movieId = movie.GetProperty("tmdb_id").GetInt32();

            MovieCache.Set(userId.Value, movieId, movieDetails);
            var message = HandlerUtils.FormatMovieShort(movieDetails);

            await HandlerUtils.SendMovieWithOptionalPoster(
                bot,
                chatId,
                movieDetails,
                message,
                cancellationToken,
                HandlerUtils.WatchedMovieButtons(userId.Value, movieId)
            );
        }

        await bot.SendMessage(chatId,
            "⬇️ Повернення до меню",
            replyMarkup: HandlerUtils.GetBackToMenuKeyboard(),
            cancellationToken: cancellationToken);
    }




    public static async Task HandleTextInput(ITelegramBotClient bot, long chatId, string userInput, string apiUrl,
        CancellationToken cancellationToken)
    {
        var userId = UserCache.GetUserId(chatId);
        if (userId == null)
        {
            Console.WriteLine($"ℹ️ userId відсутній, виконуємо авто-створення для chatId={chatId}");
            await TryFetchUserId(bot, chatId, apiUrl, cancellationToken);
            userId = UserCache.GetUserId(chatId);

            if (userId == null)
            {
                await bot.SendMessage(chatId, "❗ Не вдалося створити користувача. Спробуйте ще раз пізніше.",
                    cancellationToken: cancellationToken);
                return;
            }
        }

        var json = await HandlerUtils.GetJsonAsync($"{apiUrl}/search/{userId}?query={Uri.EscapeDataString(userInput)}");

        if (json is null)
        {
            await bot.SendMessage(chatId, "⚠️ Помилка під час пошуку фільму. Спробуйте пізніше.",
                replyMarkup: HandlerUtils.GetBackToMenuKeyboard(),
                cancellationToken: cancellationToken);
            return;
        }

        var results = json.Value.EnumerateArray().ToList();
        if (!results.Any())
        {
            await bot.SendMessage(chatId,
                "🤷‍♂️ Нічого не знайдено за вашим запитом. Спробуйте ще раз.",
                replyMarkup: HandlerUtils.GetBackToMenuKeyboard(),
                cancellationToken: cancellationToken);
            return;
        }
        
        UserInputStates.Clear(chatId);

        SearchResultsCache.Set(chatId, results);
        await ShowSearchPage(bot, chatId, userId.Value, cancellationToken);
    }

    public static async Task HandleRatingInput(ITelegramBotClient bot, long chatId, string userInput, string apiUrl, CancellationToken cancellationToken)
{
    var data = SearchResultsCache.Get(chatId);
    if (data is null || !data.Value.Movies.Any())
    {
        await bot.SendMessage(chatId, "❌ Не знайдено фільм для оцінювання", cancellationToken: cancellationToken);
        return;
    }

    var rawData = data.Value.Movies.First();

    if (!rawData.TryGetProperty("userId", out var userIdProp) || !Guid.TryParse(userIdProp.GetString(), out var parsedUserId))
    {
        await bot.SendMessage(chatId, "❌ Помилка визначення користувача", cancellationToken: cancellationToken);
        return;
    }

    if (!rawData.TryGetProperty("movieId", out var movieIdProp) || !movieIdProp.TryGetInt32(out var movieId))
    {
        await bot.SendMessage(chatId, "❌ Помилка визначення фільму", cancellationToken: cancellationToken);
        return;
    }

    if (!double.TryParse(userInput.Replace(',', '.'), out var rating) || rating < 1 || rating > 10)
    {
        await bot.SendMessage(
            chatId,
            "❗ Введіть число від 1 до 10. Наприклад: <b>8,5</b>",
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: HandlerUtils.GetBackToMenuKeyboard(),
            cancellationToken: cancellationToken
        );
        return; 
    }

    var ratingJson = new { rating };
    var response = await HandlerUtils.HttpClient.PutAsJsonAsync(
        $"{apiUrl}/movie/{parsedUserId}/{movieId}/set-rating", ratingJson, cancellationToken);

    if (response.IsSuccessStatusCode)
    {
        await bot.SendMessage(chatId, $"✅ Ваша оцінка {rating}/10 збережена!", cancellationToken: cancellationToken);

        MovieCache.UpdateUserRating(parsedUserId, movieId, rating);

        var messageId = MessageCache.Get(chatId);
        if (messageId.HasValue && MovieCache.TryGet(parsedUserId, movieId, out var updatedMovie))
        {
            var updatedCaption = HandlerUtils.FormatMovieFull(updatedMovie);

            await bot.EditMessageCaption(
                chatId: chatId,
                messageId: messageId.Value,
                caption: updatedCaption,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                replyMarkup: HandlerUtils.WatchedMovieFullButtons(parsedUserId, movieId),
                cancellationToken: cancellationToken
            );

            await bot.SendMessage(
                chatId,
                "⬇️ Повернення до меню",
                replyMarkup: HandlerUtils.GetBackToMenuKeyboard(),
                cancellationToken: cancellationToken
            );
        }
        
        MessageCache.Clear(chatId);
        SearchResultsCache.Clear(chatId);
        UserInputStates.Clear(chatId);
    }
    else
    {
        await bot.SendMessage(chatId, "❌ Не вдалося зберегти оцінку", cancellationToken: cancellationToken);
    }
}

    public static async Task ShowFilteredWatched(ITelegramBotClient bot, long chatId, string apiUrl, decimal minRating, CancellationToken cancellationToken)
    {
        var userId = UserCache.GetUserId(chatId);
        if (userId == null)
        {
            await bot.SendMessage(chatId, "⚠️ Користувача не знайдено", cancellationToken: cancellationToken);
            return;
        }

        var json = await HandlerUtils.GetJsonAsync($"{apiUrl}/movie/watched/{userId}/filter?minRating={minRating}");
        if (json is null)
        {
            await bot.SendMessage(chatId, "❌ Помилка при отриманні фільмів", cancellationToken: cancellationToken);
            return;
        }

        var movies = json.Value.EnumerateArray().ToList();
        if (!movies.Any())
        {
            await bot.SendMessage(
                chatId,
                $"😕 Немає фільмів з вашою оцінкою більше {minRating}",
                replyMarkup: HandlerUtils.GetBackToMenuKeyboard(),
                cancellationToken: cancellationToken
            );
            return;
        }


        foreach (var movie in movies)
        {
            var rawDetails = movie.GetProperty("details").GetString();
            if (string.IsNullOrWhiteSpace(rawDetails)) continue;

            var details = JsonDocument.Parse(rawDetails).RootElement;
            var movieId = movie.GetProperty("tmdb_id").GetInt32();

            MovieCache.Set(userId.Value, movieId, details);
            var message = HandlerUtils.FormatMovieShort(details);

            await HandlerUtils.SendMovieWithOptionalPoster(bot, chatId, details, message, cancellationToken,
                HandlerUtils.WatchedMovieButtons(userId.Value, movieId));
        }
        
        await bot.SendMessage(chatId,
            "⬅️ Повернути повний список",
            replyMarkup: new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "⬅️ Повернутись в головне меню", "🔁 Показати всі переглянуті" }
            }) { ResizeKeyboard = true },
            cancellationToken: cancellationToken);
    }



    
    public static async Task ShowSearchPage(ITelegramBotClient bot, long chatId, Guid userId, CancellationToken cancellationToken)
    {
        var data = SearchResultsCache.Get(chatId);
        if (data is null) return;

        var (movies, page) = data.Value;
        const int pageSize = 3;
        var pagedMovies = movies.Skip(page * pageSize).Take(pageSize).ToList();

        foreach (var movie in pagedMovies)
        {
            var message = HandlerUtils.FormatMovieShort(movie);
            var movieId = movie.GetProperty("id").GetInt32();
            var keyboard = HandlerUtils.CreateMovieButtons(userId, movieId);
            MovieCache.Set(userId, movieId, movie);

            await HandlerUtils.SendMovieWithOptionalPoster(bot, chatId, movie, message, cancellationToken, keyboard);
        }

        var maxPage = (movies.Count - 1) / pageSize;

        var keyboardButtons = new List<KeyboardButton[]>();

        var row = new List<KeyboardButton>();
        if (page > 0) row.Add(new KeyboardButton("⬅️ Попередня сторінка"));
        if (page < maxPage) row.Add(new KeyboardButton("➡️ Наступна сторінка"));
        if (row.Count > 0) keyboardButtons.Add(row.ToArray());

        keyboardButtons.Add([new KeyboardButton("⬅️ Повернутись в головне меню")]);

        var replyMarkup = new ReplyKeyboardMarkup(keyboardButtons)
        {
            ResizeKeyboard = true
        };

        await bot.SendMessage(
            chatId,
            $"📄 Сторінка {page + 1} з {maxPage + 1}",
            replyMarkup: replyMarkup,
            cancellationToken: cancellationToken);
    }

}