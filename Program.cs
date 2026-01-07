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

    static Dictionary<long, Stack<(string text, InlineKeyboardMarkup kb)>> History = new();

    static int GlobalOrderCounter = 1000;

    // ================= КНОПКИ =================

    static InlineKeyboardButton BackButton() =>
        InlineKeyboardButton.WithCallbackData("⬅️ Назад", "back");

    static InlineKeyboardMarkup WithBack(InlineKeyboardMarkup kb)
    {
        var rows = kb.InlineKeyboard.ToList();
        rows.Add(new[] { BackButton() });
        return new InlineKeyboardMarkup(rows);
    }

    static InlineKeyboardMarkup MainMenu() =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("🔥 Рейтинговая лестница / Rumble", "service_rumble") },
            new[] { InlineKeyboardButton.WithCallbackData("🎓 Тренировки / Coaching", "service_coaching") },
            new[] { InlineKeyboardButton.WithCallbackData("🆘 Помощь с рангом", "rank_help") }
        });

    static InlineKeyboardMarkup Next(string cb) =>
        new(new[] { new[] { InlineKeyboardButton.WithCallbackData("➡️ Дальше", cb) } });

    static InlineKeyboardMarkup RumbleMethod() =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("✅ Вместе с тренером", "rumble_with_coach") },
            new[] { InlineKeyboardButton.WithCallbackData("✍️ Другим способом", "rumble_other") }
        });

    static InlineKeyboardMarkup RankHelpMenu() =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("✍️ Написать вопрос здесь", "ask_question") },
            new[] { InlineKeyboardButton.WithUrl("Связаться напрямую с @bapetaype", "https://t.me/bapetaype") }
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
        new(new[] { new[] { InlineKeyboardButton.WithCallbackData("📸 Я оплатил", "paid_done") } });

    // ================= ВСПОМОГАТЕЛЬНОЕ =================

    static void Push(long chatId, string text, InlineKeyboardMarkup kb)
    {
        if (!History.ContainsKey(chatId))
            History[chatId] = new Stack<(string, InlineKeyboardMarkup)>();

        History[chatId].Push((text, kb));
    }

    static async Task Show(
        ITelegramBotClient bot,
        long chatId,
        int msgId,
        string text,
        InlineKeyboardMarkup kb,
        CancellationToken ct)
    {
        var withBack = WithBack(kb);
        Push(chatId, text, withBack);
        await bot.EditMessageText(chatId, msgId, text, replyMarkup: withBack, cancellationToken: ct);
    }

    // ================= ОБРАБОТКА =================

    static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.CallbackQuery?.Data == "back")
        {
            var chatId = update.CallbackQuery.Message!.Chat.Id;
            var msgId = update.CallbackQuery.Message.MessageId;

            if (History.TryGetValue(chatId, out var stack) && stack.Count > 1)
            {
                stack.Pop();
                var prev = stack.Peek();
                await bot.EditMessageText(chatId, msgId, prev.text,
                    replyMarkup: prev.kb, cancellationToken: ct);
            }
            return;
        }

        if (update.Message?.Text == "/start")
        {
            Push(update.Message.Chat.Id, "Главное меню", MainMenu());
            await bot.SendMessage(update.Message.Chat.Id, "Главное меню",
                replyMarkup: MainMenu(), cancellationToken: ct);
            return;
        }

        if (update.CallbackQuery == null) return;

        var cb = update.CallbackQuery;
        var chatId2 = cb.Message!.Chat.Id;
        var msg = cb.Message.MessageId;
        await bot.AnswerCallbackQuery(cb.Id, cancellationToken: ct);

        switch (cb.Data)
        {
            case "service_rumble":
                SelectedService[chatId2] = "Рейтинговая лестница / Rumble";
                await Show(bot, chatId2, msg,
                    "🏆 Рейтинговая лестница (Rumble)\n\n" +
                    "Рейтинговая лестница(он же Rumble) представляет собой временный ивент(событие) рейтинговых лиг(ранкеда) ,в котором игрокам нужно соревноваться в течении нескольких дней и удержаться в топ 9 таблицы лидеров ." +
                    "Для получения особого интерактивного полета ,цвет которого меняется в зависимости от вашего ранга,вам нужно удержаться в таблице две(2) лестницы(рамбла) в течении всего разделения(сплита) рейтинговой лиги." +
                    "Так как сложно ладдера определяется индвидуально и чем лучше статистика вашего аккаунта ,тем больше очков вам понадобится",
                    Next("rumble_method"), ct);
                break;

            case "rumble_method":
                await Show(bot, chatId2, msg, "Как вы хотите выполнить?", RumbleMethod(), ct);
                break;

            case "rumble_with_coach":
                await Show(bot, chatId2, msg, "Выберите ваш ранг:", RankSelect(), ct);
                break;

            case var r when r.StartsWith("rank_"):
                SelectedRank[chatId2] = r;
                await Show(bot, chatId2, msg, "Выберите количество очков:", PointsSelect(), ct);
                break;

            case var p when p.StartsWith("pts_"):
                SelectedPoints[chatId2] = p;
                OrderNumbers[chatId2] = ++GlobalOrderCounter;
                await Show(bot, chatId2, msg,
                    $"🧾 Заказ #{OrderNumbers[chatId2]}\n{CalculatePrice(chatId2)}",
                    PayMenu("pay"), ct);
                break;

            case "main_menu":
                await Show(bot, chatId2, msg, "Главное меню", MainMenu(), ct);
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
