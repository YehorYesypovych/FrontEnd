using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Front.Handlers;

public static class HandlerUtils
{
    public static readonly HttpClient HttpClient = new();

    public static async Task<JsonElement?> GetJsonAsync(string url)
    {
        try
        {
            var response = await HttpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(content).RootElement;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ HTTP error: {ex.Message}");
            return null;
        }
    }

    public static string FormatMovieShort(JsonElement movie)
    {
        string title = "Невідомо";

        if (movie.TryGetProperty("title", out var titleProp))
            title = titleProp.GetString() ?? title;

        if (title == "Невідомо" && movie.TryGetProperty("original_title", out var origTitleProp))
            title = origTitleProp.GetString() ?? title;

        var releaseDate = movie.TryGetProperty("release_date", out var d) && DateTime.TryParse(d.GetString(), out var dt)
            ? dt.Year.ToString()
            : "????";

        var rating = movie.TryGetProperty("vote_average", out var r) && r.TryGetDecimal(out var parsedRating)
            ? parsedRating
            : 0;

        var overview = movie.TryGetProperty("overview", out var o) ? o.GetString() ?? "" : "";
        if (overview.Length > 100)
            overview = overview[..100] + "...";

        string userRatingText = "";
        if (movie.TryGetProperty("user_rating", out var urProp) && urProp.TryGetDecimal(out var urVal))
        {
            userRatingText = $"\n🧑‍💻 Ваша оцінка: {urVal}/10";
        }

        return $"🎬 <b>{title}</b> ({releaseDate})\n⭐ Рейтинг: {rating}/10{userRatingText}\n📝 {overview}";
    }


    public static string FormatMovieFull(JsonElement movie)
    {
        var title = movie.GetProperty("title").GetString();
        var releaseDate = DateTime.TryParse(movie.GetProperty("release_date").GetString(), out var dt) ? dt.Year : 0;
        var rating = movie.GetProperty("vote_average").GetDecimal();
        var overview = movie.GetProperty("overview").GetString();

        var genreNames = new List<string>();
        if (movie.TryGetProperty("genre_ids", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var idJson in arr.EnumerateArray())
            {
                if (idJson.TryGetInt32(out var id) && GenreCache.GetName(id) is { } name)
                    genreNames.Add(name);
            }
        }
        var genresText = genreNames.Any() ? string.Join(", ", genreNames) : "Невідомо";


        string userRatingText = "";
        if (movie.TryGetProperty("user_rating", out var userRating) && userRating.TryGetDecimal(out var ur))
        {
            userRatingText = $"\n🧑‍💻 <b>Ваша оцінка:</b> {ur}/10";
        }

        return
            $"🎬 <b>{title}</b>\n" +
            $"📅 Рік: {releaseDate}\n" +
            $"⭐ Рейтинг: {rating}/10{userRatingText}\n" +
            $"🎭 Жанри: {genresText}\n" +
            $"📝 Опис: {overview}";
    }


    public static async Task SendMovieWithOptionalPoster(
        ITelegramBotClient bot,
        long chatId,
        JsonElement movieJson,
        string message,
        CancellationToken cancellationToken,
        ReplyMarkup? replyMarkup = null
        )
    {
        var posterPath = movieJson.TryGetProperty("poster_path", out var pp) ? pp.GetString() : null;

        if (!string.IsNullOrEmpty(posterPath))
        {
            var posterUrl = $"https://image.tmdb.org/t/p/w500{posterPath}";
            await bot.SendPhoto(
                chatId,
                InputFile.FromUri(posterUrl),
                caption: message,
                parseMode: ParseMode.Html,
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);
        }
        else
        {
            await bot.SendMessage(
                chatId,
                message,
                parseMode: ParseMode.Html,
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);
        }
    }

    public static InlineKeyboardMarkup CreateMovieButtons(Guid userId, int movieId)
    {
        return new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData("ℹ️ Докладніше", $"movie_details:{userId}:{movieId}")
            ],
            [
                InlineKeyboardButton.WithCallbackData("🕒 Відкласти на потім", $"movie_save:{userId}:{movieId}"),
                InlineKeyboardButton.WithCallbackData("✅ Переглянутий", $"set_watched:{userId}:{movieId}")
            ]
        ]);
    }


    public static InlineKeyboardMarkup FullInfoMovieButtons(Guid userId, int movieId)
    {
        return new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData("🔽 Згорнути", $"movie_collapse:{userId}:{movieId}"),
                InlineKeyboardButton.WithCallbackData("🕒 Відкласти на потім", $"movie_save:{userId}:{movieId}"),
                InlineKeyboardButton.WithCallbackData("✅ Переглянутий", $"set_watched:{userId}:{movieId}")
            ]
        ]);
    }



    public static InlineKeyboardMarkup SavedMovieButtons(Guid userId, int movieId)
    {
        return new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData("ℹ️ Докладніше", $"mds:{userId}:{movieId}")
            ],

            [
                InlineKeyboardButton.WithCallbackData("👁 Переглянуто", $"movie_set_watched:{userId}:{movieId}"),
                InlineKeyboardButton.WithCallbackData("🗑 Видалити", $"movie_delete:{userId}:{movieId}")
            ]
        ]);
    }

    public static InlineKeyboardMarkup SavedMovieFullButtons(Guid userId, int movieId)
    {
        return new InlineKeyboardMarkup([

            [
                InlineKeyboardButton.WithCallbackData("🔽 Згорнути", $"mcs:{userId}:{movieId}")
            ],

            [
                InlineKeyboardButton.WithCallbackData("👁 Переглянуто", $"movie_set_watched:{userId}:{movieId}"),
                InlineKeyboardButton.WithCallbackData("🗑 Видалити", $"movie_delete:{userId}:{movieId}")
            ]
        ]);
    }

    public static InlineKeyboardMarkup WatchedMovieButtons(Guid userId, int movieId)
    {
        return new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData("ℹ️ Докладніше", $"mdw:{userId}:{movieId}")
            ],
            [
                InlineKeyboardButton.WithCallbackData("⭐ Оцінити", $"movie_rate:{userId}:{movieId}"),
                InlineKeyboardButton.WithCallbackData("🗑 Видалити", $"movie_delete:{userId}:{movieId}")
            ]
        ]);
    }

    public static InlineKeyboardMarkup WatchedMovieFullButtons(Guid userId, int movieId)
    {
        return new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData("🔽 Згорнути", $"mcw:{userId}:{movieId}")
            ],

            [
                InlineKeyboardButton.WithCallbackData("⭐ Оцінити", $"movie_rate:{userId}:{movieId}"),
                InlineKeyboardButton.WithCallbackData("🗑 Видалити", $"movie_delete:{userId}:{movieId}")
            ]
        ]);
    }

    public static InlineKeyboardMarkup WatchedFilterButtons()
    {
        return new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData("🔍 Фільтрувати за оцінкою", "watched_filter")
            ]
        ]);
    }






    public static bool TryParseCallbackData(string data, string prefix, out Guid callbackUserId, out int callbackMovieId)
    {
        callbackUserId = Guid.Empty;
        callbackMovieId = 0;

        var parts = data.Split(':');
        if (parts.Length != 3 || parts[0] != prefix) return false;

        return Guid.TryParse(parts[1], out callbackUserId) &&
               int.TryParse(parts[2], out callbackMovieId);
    }
    
    public static ReplyKeyboardMarkup GetBackToMenuKeyboard() =>
        new(new[]
        {
            new[] { new KeyboardButton("⬅️ Повернутись в головне меню") }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false
        };

} 