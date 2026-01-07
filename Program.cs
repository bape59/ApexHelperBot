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

    static InlineKeyboardMarkup Next(string next, string back = "main_menu") =>
        new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⬅️ Назад", back),
                InlineKeyboardButton.WithCallbackData("➡️ Дальше", next)
            }
        });

    static InlineKeyboardMarkup BackOnly(string back) =>
        new(new[] { new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", back) } });

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
        new(new[] { new[] { InlineKeyboardButton.WithCallbackData("📸 Я оплатил", "paid_done") } });

    static InlineKeyboardMarkup RankHelpMenu() =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("✍️ Напишу вопрос здесь", "ask_manager") },
            new[] { InlineKeyboardButton.WithCallbackData("Либо свяжитесь напрямую с @bapetaype", "main_menu") }
        });

    // ================= ОБРАБОТКА =================

    static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        // ===== ВОПРОС МЕНЕДЖЕРУ =====
        if (update.Message?.Text != null && WaitingForQuestion.Contains(update.Message.Chat.Id))
        {
            WaitingForQuestion.Remove(update.Message.Chat.Id);

            await bot.SendMessage(
                MANAGER_CHAT_ID,
                $"❓ Вопрос от пользователя\nCHAT ID: {update.Message.Chat.Id}\n\n{update.Message.Text}",
                cancellationToken: ct
            );

            await bot.SendMessage(
                update.Message.Chat.Id,
                "✅ Сообщение отправлено менеджеру.",
                replyMarkup: MainMenu(),
                cancellationToken: ct
            );
            return;
        }

        // ===== СКРИНШОТ =====
        if (update.Message?.Photo != null && WaitingForScreenshot.Contains(update.Message.Chat.Id))
        {
            WaitingForScreenshot.Remove(update.Message.Chat.Id);

            await bot.ForwardMessage(
                MANAGER_CHAT_ID,
                update.Message.Chat.Id,
                update.Message.MessageId,
                cancellationToken: ct
            );

            string details =
                SelectedService[update.Message.Chat.Id] == "Тренировки / Coaching"
                    ? "1 час тренировки"
                    : $"Ранг: {SelectedRank.GetValueOrDefault(update.Message.Chat.Id)} | Очки: {SelectedPoints.GetValueOrDefault(update.Message.Chat.Id)}";

            string price =
                SelectedService[update.Message.Chat.Id] == "Тренировки / Coaching"
                    ? "1300 Р / 15$"
                    : CalculatePrice(update.Message.Chat.Id);

            await SendToGoogleSheets(
                update.Message.Chat.Id,
                OrderNumbers[update.Message.Chat.Id],
                SelectedService[update.Message.Chat.Id],
                details,
                price
            );

            await bot.SendMessage(
                update.Message.Chat.Id,
                "✅ Скриншот получен!\nМенеджер проверит оплату и свяжется с вами.",
                replyMarkup: MainMenu(),
                cancellationToken: ct
            );
            return;
        }

        if (update.Message?.Text == "/start")
        {
            await bot.SendMessage(update.Message.Chat.Id, "Главное меню", replyMarkup: MainMenu(), cancellationToken: ct);
            return;
        }

        if (update.CallbackQuery == null) return;

        var cb = update.CallbackQuery;
        var chatId = cb.Message!.Chat.Id;
        await bot.AnswerCallbackQuery(cb.Id, cancellationToken: ct);

        switch (cb.Data)
        {
            case "main_menu":
                await bot.EditMessageText(chatId, cb.Message.MessageId, "Главное меню", replyMarkup: MainMenu(), cancellationToken: ct);
                break;

            case "rank_help":
                await bot.EditMessageText(
                    chatId,
                    cb.Message.MessageId,
                    "Напишите ваш вопрос одним сообщением.\nУкажите свой контакт для связи (tg id).\nПример : напишите мне @bapetaype",
                    replyMarkup: RankHelpMenu(),
                    cancellationToken: ct
                );
                break;

            case "ask_manager":
                WaitingForQuestion.Add(chatId);
                await bot.EditMessageText(chatId, cb.Message.MessageId,
                    "Напишите ваш вопрос одним сообщением.\nУкажите свой контакт для связи (tg id).\nПример : напишите мне @bapetaype",
                    cancellationToken: ct);
                break;

            // ===== RUMBLE =====
            case "service_rumble":
                SelectedService[chatId] = "Рейтинговая лестница / Rumble";

                await bot.SendPhoto(
                    chatId,
                    new InputFileStream(File.OpenRead("rumble_points.jpg"), "rumble_points.jpg"),
                    caption:
                    "🏆 Рейтинговая лестница (Rumble)\n\n" +
                    "Рейтинговая лестница(он же Rumble) представляет собой временный ивент(событие) рейтинговых лиг(ранкеда) ,в котором игрокам нужно соревноваться в течении нескольких дней и удержаться в топ 9 таблицы лидеров ." +
                    "Для получения особого интерактивного полета ,цвет которого меняется в зависимости от вашего ранга,вам нужно удержаться в таблице две(2) лестницы(рамбла) в течении всего разделения(сплита) рейтинговой лиги." +
                    "Так как сложно ладдера определяется индвидуально и чем лучше статистика вашего аккаунта ,тем больше очков вам понадобится",
                    replyMarkup: Next("rumble_rank"),
                    cancellationToken: ct
                );
                break;

            case "rumble_rank":
                await bot.SendMessage(chatId, "Выберите ваш ранг:", replyMarkup: RankSelect(), cancellationToken: ct);
                break;

            case var r when r.StartsWith("rank_"):
                SelectedRank[chatId] = r;
                await bot.SendMessage(chatId, "Выберите количество очков:", replyMarkup: PointsSelect(), cancellationToken: ct);
                break;

            case var p when p.StartsWith("pts_"):
                SelectedPoints[chatId] = p;
                OrderNumbers[chatId] = ++GlobalOrderCounter;

                await bot.SendMessage(
                    chatId,
                    $"🧾 Заказ #{OrderNumbers[chatId]}\n{CalculatePrice(chatId)}",
                    replyMarkup: PayMenu("rumble_pay"),
                    cancellationToken: ct
                );
                break;

            // ===== COACHING =====
            case "service_coaching":
                SelectedService[chatId] = "Тренировки / Coaching";
                await bot.EditMessageText(
                    chatId,
                    cb.Message.MessageId,
                    "Тренировочный процесс представялет собой просмотр(разбор) ваших записей игр (демок) и игра вместе с тренером корректирующим вас и ваши ошибки",
                    replyMarkup: Next("coach_price"),
                    cancellationToken: ct
                );
                break;

            case "coach_price":
                OrderNumbers[chatId] = ++GlobalOrderCounter;
                await bot.EditMessageText(
                    chatId,
                    cb.Message.MessageId,
                    $"Стоимость 1-го часа тренировки состовялет 1300 Р или 15$\n\n🧾 Заказ #{OrderNumbers[chatId]}",
                    replyMarkup: PayMenu("coach_pay"),
                    cancellationToken: ct
                );
                break;

            case "rumble_pay":
            case "coach_pay":
                await bot.SendMessage(
                    chatId,
                    "💳 Реквизиты:\n\nСБП: 79964821339\nКрипта / PayPal — @bapetaype\n\nПосле оплаты нажмите «📸 Я оплатил»",
                    replyMarkup: AfterPay(),
                    cancellationToken: ct
                );
                break;

            case "paid_done":
                WaitingForScreenshot.Add(chatId);
                await bot.EditMessageText(chatId, cb.Message.MessageId, "📸 Пришлите скриншот оплаты.", cancellationToken: ct);
                break;
        }
    }

    static string CalculatePrice(long chatId)
    {
        var r = SelectedRank[chatId];
        var p = SelectedPoints[chatId];

        if (r == "rank_master")
            return "🔴 MASTER+\n💰 От 10 000 ₽\n👥 2 игрока\n⚠️ Только pred-лобби";

        return "💰 Стоимость рассчитывается индивидуально";
    }

    static async Task SendToGoogleSheets(long chatId, int orderId, string service, string details, string price)
    {
        using var client = new HttpClient();

        var payload = new
        {
            chat_id = chatId,
            service = service,
            details = $"Заказ #{orderId} | {details}",
            price = price
        };

        var json = JsonSerializer.Serialize(payload);
        await client.PostAsync(
            GOOGLE_SHEETS_URL,
            new StringContent(json, Encoding.UTF8, "application/json")
        );
    }

    static Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        Console.WriteLine(ex);
        return Task.CompletedTask;
    }
}
