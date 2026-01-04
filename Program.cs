using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

class Program
{
    const string MANAGER_USERNAME = "@bapetaype";

    static Dictionary<long, string> SelectedRank = new();
    static Dictionary<long, string> SelectedPoints = new();
    static HashSet<long> WaitingForScreenshot = new();

    static async Task Main()
    {
        var token = Environment.GetEnvironmentVariable("BOT_TOKEN")
            ?? throw new Exception("BOT_TOKEN not set");

        var bot = new TelegramBotClient(token);
        using var cts = new CancellationTokenSource();

        bot.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() },
            cts.Token
        );

        Console.WriteLine($"Бот запущен: @{(await bot.GetMe()).Username}");
        await Task.Delay(-1);
    }

    // ================= КНОПКИ =================

    static InlineKeyboardMarkup MainMenu() =>
        new(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("🔥 Рейтинговая лестница / Rumble", "service_rumble") },
            new [] { InlineKeyboardButton.WithCallbackData("🆘 Помощь с рангом", "rank_help") }
        });

    static InlineKeyboardMarkup RumbleStart() =>
        new(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("🤝 Вместе", "rumble_together") },
            new [] { InlineKeyboardButton.WithCallbackData("⬅️ Главное меню", "main_menu") }
        });

    static InlineKeyboardMarkup RankSelect() =>
        new(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("🟡 GOLD и ниже", "rank_gold") },
            new [] { InlineKeyboardButton.WithCallbackData("🔵 PLAT", "rank_plat") },
            new [] { InlineKeyboardButton.WithCallbackData("🟣 DIAMOND", "rank_diamond") },
            new [] { InlineKeyboardButton.WithCallbackData("🔴 MASTER+", "rank_master") },
        });

    static InlineKeyboardMarkup PointsSelect() =>
        new(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("≤ 1500", "pts_low") },
            new [] { InlineKeyboardButton.WithCallbackData("1500–2000", "pts_mid") },
            new [] { InlineKeyboardButton.WithCallbackData("2000+", "pts_high") },
        });

    static InlineKeyboardMarkup AfterPrice() =>
        new(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("💳 Оплатить", "rumble_pay") },
            new [] { InlineKeyboardButton.WithCallbackData("❓ Остались вопросы", "rumble_info") },
            new [] { InlineKeyboardButton.WithCallbackData("⬅️ Главное меню", "main_menu") }
        });

    static InlineKeyboardMarkup AfterPay() =>
        new(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("📸 Я оплатил", "rumble_paid") },
            new [] { InlineKeyboardButton.WithCallbackData("⬅️ Главное меню", "main_menu") }
        });

    // ================= ОБРАБОТКА =================

    static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        // === Приём скриншота ===
        if (update.Type == UpdateType.Message &&
            update.Message!.Photo != null &&
            WaitingForScreenshot.Contains(update.Message.Chat.Id))
        {
            await ForwardScreenshotToManager(bot, update.Message, ct);
            WaitingForScreenshot.Remove(update.Message.Chat.Id);

            await bot.SendMessage(
                update.Message.Chat.Id,
                "✅ Скриншот получен!\nМенеджер проверит оплату и свяжется с вами.",
                cancellationToken: ct
            );
            return;
        }

        if (update.Type == UpdateType.Message && update.Message?.Text == "/start")
        {
            await bot.SendMessage(update.Message.Chat.Id, "Главное меню", replyMarkup: MainMenu(), cancellationToken: ct);
            return;
        }

        if (update.Type != UpdateType.CallbackQuery) return;

        var cb = update.CallbackQuery!;
        var chatId = cb.Message!.Chat.Id;
        var user = cb.From;

        await bot.AnswerCallbackQuery(cb.Id, cancellationToken: ct);

        switch (cb.Data)
        {
            case "main_menu":
                await bot.SendMessage(chatId, "Главное меню", replyMarkup: MainMenu(), cancellationToken: ct);
                break;

            case "service_rumble":
                await bot.SendMessage(
                    chatId,
                    "🏆 Рейтинговая лестница (Rumble)\n\n" +
                    "Временный ивент ранкеда.\n" +
                    "Необходимо удержаться в ТОП-9 таблицы лидеров.\n\n" +
                    "Для получения интерактивного полёта нужно удержать 2 рамбла за сплит.",
                    replyMarkup: RumbleStart(),
                    cancellationToken: ct
                );
                break;

            case "rumble_together":
                await bot.SendMessage(chatId, "Выберите ваш ранг:", replyMarkup: RankSelect(), cancellationToken: ct);
                break;

            case string r when r.StartsWith("rank_"):
                SelectedRank[chatId] = r;
                await bot.SendMessage(chatId, "Выберите очки:", replyMarkup: PointsSelect(), cancellationToken: ct);
                break;

            case string p when p.StartsWith("pts_"):
                SelectedPoints[chatId] = p;
                await bot.SendMessage(chatId, CalculatePrice(chatId), replyMarkup: AfterPrice(), cancellationToken: ct);
                break;

            case "rumble_pay":
                await SendOrderToManager(bot, chatId, user, ct);
                await bot.SendMessage(
                    chatId,
                    "💳 Реквизиты:\n\n" +
                    "СБП: 79964821339\n" +
                    "Крипта / PayPal — @bapetaype\n\n" +
                    "После оплаты нажмите «📸 Я оплатил» и пришлите скриншот.",
                    replyMarkup: AfterPay(),
                    cancellationToken: ct
                );
                break;

            case "rumble_paid":
                WaitingForScreenshot.Add(chatId);
                await bot.SendMessage(
                    chatId,
                    "📸 Пришлите скриншот оплаты одним изображением.",
                    cancellationToken: ct
                );
                break;

            case "rumble_info":
            case "rank_help":
                await bot.SendMessage(chatId, "Свяжитесь 👉 @bapetaype", cancellationToken: ct);
                break;
        }
    }

    // ================= МЕНЕДЖЕР =================

    static async Task SendOrderToManager(ITelegramBotClient bot, long chatId, User user, CancellationToken ct)
    {
        string rank = SelectedRank[chatId].Replace("rank_", "").ToUpper();
        string pts = SelectedPoints[chatId] switch
        {
            "pts_low" => "≤1500",
            "pts_mid" => "1500–2000",
            _ => "2000+"
        };

        await bot.SendMessage(
            MANAGER_USERNAME,
            $"🆕 Новая заявка RUMBLE\n\n" +
            $"👤 @{user.Username ?? "без username"}\n" +
            $"🆔 {chatId}\n\n" +
            $"🏅 {rank}\n📊 {pts}\n\n" +
            $"⏰ {DateTime.Now:dd.MM.yyyy HH:mm}",
            cancellationToken: ct
        );
    }

    static async Task ForwardScreenshotToManager(ITelegramBotClient bot, Message msg, CancellationToken ct)
    {
        await bot.ForwardMessage(
            MANAGER_USERNAME,
            msg.Chat.Id,
            msg.MessageId,
            cancellationToken: ct
        );
    }

    // ================= ЦЕНА =================

    static string CalculatePrice(long chatId)
    {
        var r = SelectedRank[chatId];
        var p = SelectedPoints[chatId];

        if (r == "rank_master")
            return "🔴 MASTER+\n💰 От 10 000 ₽\n👥 2 игрока\n⚠️ Только pred-лобби";

        int price = r switch
        {
            "rank_gold" => p == "pts_low" ? 3000 : p == "pts_mid" ? 4000 : 5000,
            "rank_plat" => p == "pts_low" ? 4000 : p == "pts_mid" ? 5000 : 6000,
            _ => p == "pts_low" ? 5000 : p == "pts_mid" ? 6500 : 7500
        };

        string party = r == "rank_diamond" ? "2 игрока" : "1 игрок";

        return $"💰 Стоимость: {price} ₽\n👥 Пати: {party}";
    }

    static Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        Console.WriteLine(ex);
        return Task.CompletedTask;
    }
}
