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
    static Dictionary<long, string> LastStep = new();

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

    static InlineKeyboardMarkup WithBack(string next) =>
        new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⬅️ Назад", "back"),
                InlineKeyboardButton.WithCallbackData("➡️ Дальше", next)
            }
        });

    static InlineKeyboardMarkup BackOnly() =>
        new(new[] { new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "back") } });

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
            new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "back") }
        });

    static InlineKeyboardMarkup AfterPay() =>
        new(new[] { new[] { InlineKeyboardButton.WithCallbackData("📸 Я оплатил", "paid_done") } });

    static InlineKeyboardMarkup RankHelpMenu() =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("✍️ Напишу вопрос здесь", "ask_manager") },
            new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "back") }
        });

    // ================= ОБРАБОТКА =================

    static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message?.Photo != null && WaitingForScreenshot.Contains(update.Message.Chat.Id))
        {
            long chatId = update.Message.Chat.Id;
            WaitingForScreenshot.Remove(chatId);

            await bot.ForwardMessage(MANAGER_CHAT_ID, chatId, update.Message.MessageId, cancellationToken: ct);

            string details = SelectedService[chatId] == "Тренировки / Coaching"
                ? "1 час тренировки"
                : $"Ранг: {SelectedRank[chatId]} | Очки: {SelectedPoints[chatId]}";

            string price = SelectedService[chatId] == "Тренировки / Coaching"
                ? "1300 Р / 15$"
                : CalculatePrice(chatId);

            await SendToGoogleSheets(chatId, OrderNumbers[chatId], SelectedService[chatId], details, price);

            await bot.SendMessage(chatId,
                "✅ Скриншот получен!\nМенеджер проверит оплату и свяжется с вами.",
                replyMarkup: MainMenu(),
                cancellationToken: ct);
            return;
        }

        if (update.CallbackQuery == null) return;

        var cb = update.CallbackQuery;
        var chatId2 = cb.Message!.Chat.Id;
        await bot.AnswerCallbackQuery(cb.Id, cancellationToken: ct);

        if (cb.Data == "back" && LastStep.ContainsKey(chatId2))
        {
            await HandleFakeCallback(bot, chatId2, LastStep[chatId2], ct);
            return;
        }

        LastStep[chatId2] = cb.Data;

        switch (cb.Data)
        {
            case "service_rumble":
                SelectedService[chatId2] = "Рейтинговая лестница / Rumble";
                await bot.SendPhoto(chatId2,
                    new InputFileStream(File.OpenRead("rumble_points.jpg"), "rumble_points.jpg"),
                    caption:
                    "🏆 Рейтинговая лестница (Rumble)\n\n" +
                    "Рейтинговая лестница(он же Rumble) представляет собой временный ивент(событие) рейтинговых лиг(ранкеда) ,в котором игрокам нужно соревноваться в течении нескольких дней и удержаться в топ 9 таблицы лидеров ." +
                    "Для получения особого интерактивного полета ,цвет которого меняется в зависимости от вашего ранга,вам нужно удержаться в таблице две(2) лестницы(рамбла) в течении всего разделения(сплита) рейтинговой лиги." +
                    "Так как сложно ладдера определяется индвидуально и чем лучше статистика вашего аккаунта ,тем больше очков вам понадобится",
                    replyMarkup: WithBack("rumble_rank"),
                    cancellationToken: ct);
                break;

            case "rumble_rank":
                await bot.SendMessage(chatId2, "Выберите ваш ранг:", replyMarkup: RankSelect(), cancellationToken: ct);
                break;

            case var r when r.StartsWith("rank_"):
                SelectedRank[chatId2] = r;
                await bot.SendMessage(chatId2, "Выберите количество очков:", replyMarkup: PointsSelect(), cancellationToken: ct);
                break;

            case var p when p.StartsWith("pts_"):
                SelectedPoints[chatId2] = p;
                OrderNumbers[chatId2] = ++GlobalOrderCounter;

                await bot.SendMessage(chatId2,
                    $"🧾 Заказ #{OrderNumbers[chatId2]}\n{CalculatePrice(chatId2)}",
                    replyMarkup: PayMenu("rumble_pay"),
                    cancellationToken: ct);
                break;

            case "rumble_pay":
                await bot.SendMessage(chatId2,
                    "💳 Реквизиты:\n\nСБП: 79964821339\nКрипта / PayPal — @bapetaype\n\nПосле оплаты нажмите «📸 Я оплатил»",
                    replyMarkup: AfterPay(),
                    cancellationToken: ct);
                break;

            case "paid_done":
                WaitingForScreenshot.Add(chatId2);
                await bot.SendMessage(chatId2, "📸 Пришлите скриншот оплаты.", replyMarkup: BackOnly(), cancellationToken: ct);
                break;
        }
    }

    static string CalculatePrice(long chatId)
    {
        return $"{SelectedRank[chatId]} | {SelectedPoints[chatId]}\n💰 Стоимость: рассчитывается автоматически";
    }

    static async Task SendToGoogleSheets(long chatId, int orderId, string service, string details, string price)
    {
        using var client = new HttpClient();
        var payload = new { chat_id = chatId, service, details = $"Заказ #{orderId} | {details}", price };
        var json = JsonSerializer.Serialize(payload);
        await client.PostAsync(GOOGLE_SHEETS_URL, new StringContent(json, Encoding.UTF8, "application/json"));
    }

    static async Task HandleFakeCallback(ITelegramBotClient bot, long chatId, string data, CancellationToken ct)
    {
        await HandleUpdateAsync(bot,
            new Update { CallbackQuery = new CallbackQuery { Data = data, Message = new Message { Chat = new Chat { Id = chatId } } } },
            ct);
    }

    static Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        Console.WriteLine(ex);
        return Task.CompletedTask;
    }
}
