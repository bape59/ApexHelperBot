using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.IO;

class Program
{
    const long MANAGER_CHAT_ID = 6312652767;
    const string GOOGLE_SHEETS_URL =
        "https://script.google.com/macros/s/AKfycbzKoFvDQFPcmFvFo5sMHTjH2IwfQ8EvJS-jijn2JsdO4MlXQdJY2_EN1sYhEQCLKU47/exec";

    static Dictionary<long, string> SelectedRank = new();
    static Dictionary<long, string> SelectedPoints = new();
    static Dictionary<long, string> SelectedService = new();
    static Dictionary<long, int> OrderNumbers = new();

    static HashSet<long> WaitingForScreenshot = new();
    static HashSet<long> WaitingForQuestion = new();

    static int GlobalOrderCounter = 1000;

    static async Task Main(string[] args)
    {
        var token = Environment.GetEnvironmentVariable("BOT_TOKEN")
            ?? throw new Exception("BOT_TOKEN not set");

        var bot = new TelegramBotClient(token);
        using var cts = new CancellationTokenSource();

        bot.StartReceiving(HandleUpdateAsync, HandleErrorAsync,
            new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() }, cts.Token);

        Console.WriteLine($"Бот запущен: @{(await bot.GetMe()).Username}");
        await Task.Delay(-1);
    }

    // ================= КНОПКИ =================

    static InlineKeyboardButton Back(string to) =>
        InlineKeyboardButton.WithCallbackData("⬅️ Назад", to);

    static InlineKeyboardMarkup MainMenu() =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("🔥 Рейтинговая лестница / Rumble", "service_rumble") },
            new[] { InlineKeyboardButton.WithCallbackData("🎓 Тренировки / Coaching", "service_coaching") },
            new[] { InlineKeyboardButton.WithCallbackData("🆘 Помощь с рангом", "rank_help") }
        });

    static InlineKeyboardMarkup Next(string next, string back) =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("➡️ Дальше", next) },
            new[] { Back(back) }
        });

    static InlineKeyboardMarkup RumbleMethod() =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("✅ Вместе с тренером", "rumble_with_coach") },
            new[] { InlineKeyboardButton.WithCallbackData("✍️ Другим способом", "rumble_other") },
            new[] { Back("main_menu") }
        });

    static InlineKeyboardMarkup RankHelpMenu() =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("✍️ Написать вопрос здесь", "ask_question") },
            new[] { InlineKeyboardButton.WithUrl("Связаться напрямую с @bapetaype", "https://t.me/bapetaype") },
            new[] { Back("main_menu") }
        });

    static InlineKeyboardMarkup RankSelect() =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("🟡 GOLD и ниже", "rank_gold") },
            new[] { InlineKeyboardButton.WithCallbackData("🔵 PLAT", "rank_plat") },
            new[] { InlineKeyboardButton.WithCallbackData("🟣 DIAMOND", "rank_diamond") },
            new[] { InlineKeyboardButton.WithCallbackData("🔴 MASTER+", "rank_master") },
            new[] { Back("rumble_method") }
        });

    static InlineKeyboardMarkup PointsSelect() =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("≤ 1500", "pts_low") },
            new[] { InlineKeyboardButton.WithCallbackData("1500–2000", "pts_mid") },
            new[] { InlineKeyboardButton.WithCallbackData("2000+", "pts_high") },
            new[] { Back("rumble_with_coach") }
        });

    static InlineKeyboardMarkup PayMenu(string back) =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("💳 Получить реквизиты", "pay") },
            new[] { Back(back) }
        });

    static InlineKeyboardMarkup AfterPay() =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("📸 Я оплатил", "paid_done") },
            new[] { Back("main_menu") }
        });

    // ================= ОБРАБОТКА =================

    static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message?.Text == "/start")
        {
            await bot.SendMessage(update.Message.Chat.Id, "Главное меню",
                replyMarkup: MainMenu(), cancellationToken: ct);
            return;
        }

        if (update.CallbackQuery == null) return;

        var cb = update.CallbackQuery;
        var chatId = cb.Message!.Chat.Id;
        await bot.AnswerCallbackQuery(cb.Id, cancellationToken: ct);

        switch (cb.Data)
        {
            case "main_menu":
                await bot.SendMessage(chatId, "Главное меню",
                    replyMarkup: MainMenu(), cancellationToken: ct);
                break;

            case "rank_help":
                await bot.SendMessage(chatId,
                    "Напишите ваш вопрос одним сообщением.\nУкажите свой контакт (tg id).\nПример: напишите мне @bapetaype",
                    replyMarkup: RankHelpMenu(), cancellationToken: ct);
                break;

            case "ask_question":
                WaitingForQuestion.Add(chatId);
                await bot.SendMessage(chatId,
                    "Напишите ваш вопрос одним сообщением.\nУкажите свой контакт (tg id).\nПример: напишите мне @bapetaype",
                    replyMarkup: RankHelpMenu(), cancellationToken: ct);
                break;

            case "service_rumble":
                SelectedService[chatId] = "Рейтинговая лестница / Rumble";
                await bot.SendPhoto(
                    chatId,
                    new InputFileStream(File.OpenRead("rumble_points.jpg")),
                    caption:
                    "🏆 Рейтинговая лестница (Rumble)\n\n" +
                    "Рейтинговая лестница(он же Rumble) представляет собой временный ивент(событие) рейтинговых лиг(ранкеда) ,в котором игрокам нужно соревноваться в течении нескольких дней и удержаться в топ 9 таблицы лидеров ." +
                    "Для получения особого интерактивного полета ,цвет которого меняется в зависимости от вашего ранга,вам нужно удержаться в таблице две(2) лестницы(рамбла) в течении всего разделения(сплита) рейтинговой лиги." +
                    "Так как сложно ладдера определяется индвидуально и чем лучше статистика вашего аккаунта ,тем больше очков вам понадобится",
                    replyMarkup: Next("rumble_method", "main_menu"),
                    cancellationToken: ct
                );
                break;

            case "rumble_method":
                await bot.SendMessage(chatId, "Как вы хотите выполнить?",
                    replyMarkup: RumbleMethod(), cancellationToken: ct);
                break;

            case "rumble_with_coach":
                await bot.SendMessage(chatId, "Выберите ваш ранг:",
                    replyMarkup: RankSelect(), cancellationToken: ct);
                break;

            case var r when r.StartsWith("rank_"):
                SelectedRank[chatId] = r;
                await bot.SendMessage(chatId, "Выберите количество очков:",
                    replyMarkup: PointsSelect(), cancellationToken: ct);
                break;

            case var p when p.StartsWith("pts_"):
                SelectedPoints[chatId] = p;
                OrderNumbers[chatId] = ++GlobalOrderCounter;
                await bot.SendMessage(chatId,
                    $"🧾 Заказ #{OrderNumbers[chatId]}\n{CalculatePrice(chatId)}",
                    replyMarkup: PayMenu("rumble_with_coach"), cancellationToken: ct);
                break;

            case "pay":
                await bot.SendMessage(chatId,
                    "💳 Реквизиты:\n\nСБП: 79964821339\nКрипта / PayPal — @bapetaype\n\nПосле оплаты нажмите «📸 Я оплатил»",
                    replyMarkup: AfterPay(), cancellationToken: ct);
                break;

            case "paid_done":
                WaitingForScreenshot.Add(chatId);
                await bot.SendMessage(chatId,
                    "📸 Пришлите скриншот оплаты.",
                    replyMarkup: AfterPay(), cancellationToken: ct);
                break;
        }
    }

    static string CalculatePrice(long chatId)
    {
        var r = SelectedRank.GetValueOrDefault(chatId);
        var p = SelectedPoints.GetValueOrDefault(chatId);

        if (r == "rank_master")
            return "🔴 MASTER+\n💰 От 10 000 ₽\n👥 2 игрока\n⚠️ Только pred-лобби";

        int price = r switch
        {
            "rank_gold" => p == "pts_low" ? 3000 : p == "pts_mid" ? 4000 : 5000,
            "rank_plat" => p == "pts_low" ? 4000 : p == "pts_mid" ? 5500 : 7000,
            "rank_diamond" => p == "pts_low" ? 6000 : p == "pts_mid" ? 8000 : 10000,
            _ => 0
        };

        return $"💰 Стоимость: {price} ₽";
    }

    static Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        Console.WriteLine(ex);
        return Task.CompletedTask;
    }
}
