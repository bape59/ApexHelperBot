using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

class Program
{
    // ⬇️ ПОКА ПУСТО — ЗАПОЛНИМ ПОСЛЕ ПРОВЕРКИ
    const long MANAGER_CHAT_ID = 6312652767;


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
            new[] { InlineKeyboardButton.WithCallbackData("🔥 Рейтинговая лестница / Rumble", "service_rumble") },
            new[] { InlineKeyboardButton.WithCallbackData("🎓 Тренировки / Coaching", "service_coaching") },
            new[] { InlineKeyboardButton.WithCallbackData("🆘 Помощь с рангом", "rank_help") }
        });

    static InlineKeyboardMarkup Next(string cb) =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("➡️ Дальше", cb) }
        });

    static InlineKeyboardMarkup RankSelect() =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("🟡 GOLD и ниже", "rank_gold") },
            new[] { InlineKeyboardButton.WithCallbackData("🔵 PLAT", "rank_plat") },
            new[] { InlineKeyboardButton.WithCallbackData("🟣 DIAMOND", "rank_diamond") },
            new[] { InlineKeyboardButton.WithCallbackData("🔴 MASTER+", "rank_master") }
        });

    static InlineKeyboardMarkup PointsSelect() =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("≤ 1500", "pts_low") },
            new[] { InlineKeyboardButton.WithCallbackData("1500–2000", "pts_mid") },
            new[] { InlineKeyboardButton.WithCallbackData("2000+", "pts_high") }
        });

    static InlineKeyboardMarkup PayMenu(string cb) =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("💳 Получить реквизиты", cb) },
            new[] { InlineKeyboardButton.WithCallbackData("⬅️ Главное меню", "main_menu") }
        });

    static InlineKeyboardMarkup AfterPay() =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("📸 Я оплатил", "paid_done") }
        });

    // ================= ОБРАБОТКА =================

    static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        // 🔴 ВЫВОД CHAT ID (ГЛАВНОЕ ДЛЯ ТЕБЯ СЕЙЧАС)
        if (update.Message?.Photo != null &&
            WaitingForScreenshot.Contains(update.Message.Chat.Id))
        {
            // 👉 пересылаем скриншот тебе
            await bot.ForwardMessage(
                MANAGER_CHAT_ID,
                update.Message.Chat.Id,
                update.Message.MessageId,
                cancellationToken: ct
            );

            WaitingForScreenshot.Remove(update.Message.Chat.Id);

            await bot.SendMessage(
                update.Message.Chat.Id,
                "✅ Скриншот получен!\nМенеджер проверит оплату и свяжется с вами.",
                cancellationToken: ct
            );
            return;
        }


        if (update.Message?.Text == "/start")
        {
            await bot.SendMessage(
                update.Message.Chat.Id,
                "Главное меню",
                replyMarkup: MainMenu(),
                cancellationToken: ct
            );
            return;
        }

        if (update.CallbackQuery == null) return;

        var cb = update.CallbackQuery;
        var chatId = cb.Message!.Chat.Id;

        await bot.AnswerCallbackQuery(cb.Id, cancellationToken: ct);

        switch (cb.Data)
        {
            case "main_menu":
                await bot.EditMessageText(
                    chatId,
                    cb.Message.MessageId,
                    "Главное меню",
                    replyMarkup: MainMenu(),
                    cancellationToken: ct
                );
                break;

            // ===== RUMBLE =====
            case "service_rumble":
                await bot.EditMessageText(
                    chatId,
                    cb.Message.MessageId,
                    "🏆 Рейтинговая лестница (Rumble)\n\n" +
                    "Рейтинговая лестница(он же Rumble) представляет собой временный ивент(событие) рейтинговых лиг(ранкеда) ,в котором игрокам нужно соревноваться в течении нескольких дней и удержаться в топ 9 таблицы лидеров ." +
                    "Для получения особого интерактивного полета ,цвет которого меняется в зависимости от вашего ранга,вам нужно удержаться в таблице две(2) лестницы(рамбла) в течении всего разделения(сплита) рейтинговой лиги." +
                    "Так как сложно ладдера определяется индвидуально и чем лучше статистика вашего аккаунта ,тем больше очков вам понадобится",
                    replyMarkup: Next("rumble_rank"),
                    cancellationToken: ct
                );
                break;

            case "rumble_rank":
                await bot.EditMessageText(
                    chatId,
                    cb.Message.MessageId,
                    "Выберите ваш ранг:",
                    replyMarkup: RankSelect(),
                    cancellationToken: ct
                );
                break;

            case var r when r.StartsWith("rank_"):
                SelectedRank[chatId] = r;
                await bot.EditMessageText(
                    chatId,
                    cb.Message.MessageId,
                    "Выберите количество очков:",
                    replyMarkup: PointsSelect(),
                    cancellationToken: ct
                );
                break;

            case var p when p.StartsWith("pts_"):
                SelectedPoints[chatId] = p;
                await bot.EditMessageText(
                    chatId,
                    cb.Message.MessageId,
                    CalculatePrice(chatId),
                    replyMarkup: PayMenu("rumble_pay"),
                    cancellationToken: ct
                );
                break;

            case "rumble_pay":
            case "coach_pay":
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

            case "paid_done":
                WaitingForScreenshot.Add(chatId);
                await bot.EditMessageText(
                    chatId,
                    cb.Message.MessageId,
                    "📸 Пришлите скриншот оплаты одним изображением.",
                    cancellationToken: ct
                );
                break;

            // ===== COACHING =====
            case "service_coaching":
                await bot.EditMessageText(
                    chatId,
                    cb.Message.MessageId,
                    "Тренировочный процесс представялет собой просмотр(разбор) ваших записей игр (демок) и игра вместе с тренером корректирующим вас и ваши ошибки",
                    replyMarkup: Next("coach_price"),
                    cancellationToken: ct
                );
                break;

            case "coach_price":
                await bot.EditMessageText(
                    chatId,
                    cb.Message.MessageId,
                    "Стоимость одного часа тренировки 1300 рублей / 15$",
                    replyMarkup: PayMenu("coach_pay"),
                    cancellationToken: ct
                );
                break;

            case "rank_help":
                await bot.EditMessageText(
                    chatId,
                    cb.Message.MessageId,
                    "для уточнения данной услуги свяжитесь с менеджером @bapetaype",
                    replyMarkup: MainMenu(),
                    cancellationToken: ct
                );
                break;
        }
    }

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
