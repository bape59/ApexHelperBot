using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

class Program
{
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

        var me = await bot.GetMe();
        Console.WriteLine($"Бот запущен: @{me.Username}");

        // важно для Railway
        await Task.Delay(-1);
    }

    // ================= МЕНЮ =================

    static async Task SendMainMenu(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        var menu = new InlineKeyboardMarkup(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("💪 Тренировки / Coaching", "training_info") },
            new [] { InlineKeyboardButton.WithCallbackData("🔥 Рейтинговая лестница / Rumble", "service_rumble") },
            new [] { InlineKeyboardButton.WithCallbackData("🆘 Помощь с рангом", "rank_help") }
        });

        await bot.SendMessage(
            chatId,
            "Главное меню 👋\nВыберите услугу:",
            replyMarkup: menu,
            cancellationToken: ct
        );
    }

    // ================= КНОПКИ =================

    static InlineKeyboardMarkup TrainingStart() =>
        new InlineKeyboardMarkup(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("💪 Хочу тренировку", "training_want") },
            new [] { InlineKeyboardButton.WithCallbackData("⬅️ Главное меню", "main_menu") }
        });

    static InlineKeyboardMarkup AgreeButton() =>
        new InlineKeyboardMarkup(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("✅ Согласен", "training_agree") },
            new [] { InlineKeyboardButton.WithCallbackData("⬅️ Главное меню", "main_menu") }
        });

    static InlineKeyboardMarkup PaymentButtons() =>
        new InlineKeyboardMarkup(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("✅ Подтвердить", "confirm_payment") },
            new [] { InlineKeyboardButton.WithCallbackData("⬅️ Главное меню", "main_menu") }
        });

    static InlineKeyboardMarkup RumbleStart() =>
        new InlineKeyboardMarkup(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("🔥 Хочу рамбл", "rumble_want") },
            new [] { InlineKeyboardButton.WithCallbackData("⬅️ Главное меню", "main_menu") }
        });

    static InlineKeyboardMarkup RumbleMethod() =>
        new InlineKeyboardMarkup(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("🤝 Вместе", "rumble_together") },
            new [] { InlineKeyboardButton.WithCallbackData("💸 Другим способом", "rumble_cheaper") },
            new [] { InlineKeyboardButton.WithCallbackData("⬅️ Главное меню", "main_menu") }
        });

    static InlineKeyboardMarkup UnderstoodButton() =>
        new InlineKeyboardMarkup(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("✅ Понял", "rank_help_ok") }
        });

    // ================= ОБРАБОТКА =================

    static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Type == UpdateType.Message && update.Message?.Text == "/start")
        {
            await SendMainMenu(bot, update.Message.Chat.Id, ct);
            return;
        }

        if (update.Type != UpdateType.CallbackQuery || update.CallbackQuery == null)
            return;

        var cb = update.CallbackQuery;
        var chatId = cb.Message!.Chat.Id;

        await bot.AnswerCallbackQuery(cb.Id, cancellationToken: ct);

        switch (cb.Data)
        {
            case "main_menu":
                await SendMainMenu(bot, chatId, ct);
                break;

            // ========= ПОМОЩЬ С РАНГОМ =========

            case "rank_help":
                await bot.SendMessage(
                    chatId,
                    "Чтобы ознакомиться с услугами помощи свяжитесь с 👉 @bapetaype",
                    replyMarkup: UnderstoodButton(),
                    cancellationToken: ct
                );
                break;

            case "rank_help_ok":
                await bot.SendMessage(
                    chatId,
                    "Ждём вашего обращения и будем рады помочь 🙌",
                    replyMarkup: new InlineKeyboardMarkup(
                        InlineKeyboardButton.WithCallbackData("⬅️ Главное меню", "main_menu")
                    ),
                    cancellationToken: ct
                );
                break;

            // ========= ТРЕНИРОВКИ =========

            case "training_info":
                await bot.SendMessage(
                    chatId,
                    "Тренировочный процесс:\n\n" +
                    "Разбор демо, выявление ошибок и совместная игра.\n" +
                    "Тренер корректирует действия в реальном времени.",
                    replyMarkup: TrainingStart(),
                    cancellationToken: ct
                );
                break;

            case "training_want":
                await bot.SendMessage(
                    chatId,
                    "Стоимость 1 часа:\n\n💰 1300 рублей / 15$",
                    replyMarkup: AgreeButton(),
                    cancellationToken: ct
                );
                break;

            case "training_agree":
                await bot.SendMessage(
                    chatId,
                    "Реквизиты:\n\n" +
                    "1) СБП: 79964821339\n" +
                    "2) Крипта / PayPal — в ЛС 👉 @bapetaype\n\n" +
                    "После оплаты нажмите ✅ Подтвердить",
                    replyMarkup: PaymentButtons(),
                    cancellationToken: ct
                );
                break;

            // ========= RUMBLE =========

            case "service_rumble":
                await bot.SendMessage(
                    chatId,
                    "Rumble — временный ивент ранкеда.\n\n" +
                    "5 лучших игр → место в таблице.\n" +
                    "Топ 9 получают интерактивный полёт.",
                    replyMarkup: RumbleStart(),
                    cancellationToken: ct
                );
                break;

            case "rumble_want":
                await bot.SendMessage(
                    chatId,
                    "Как ты хочешь это сделать?",
                    replyMarkup: RumbleMethod(),
                    cancellationToken: ct
                );
                break;

            case "rumble_together":
                await bot.SendMessage(
                    chatId,
                    "💰 10 000 рублей за задание\n\n" +
                    "После оплаты нажмите ✅ Подтвердить",
                    replyMarkup: PaymentButtons(),
                    cancellationToken: ct
                );
                break;

            case "rumble_cheaper":
                await bot.SendMessage(
                    chatId,
                    "Для уточнения напишите 👉 @bapetaype",
                    replyMarkup: new InlineKeyboardMarkup(
                        InlineKeyboardButton.WithCallbackData("⬅️ Главное меню", "main_menu")
                    ),
                    cancellationToken: ct
                );
                break;

            case "confirm_payment":
                await bot.SendMessage(
                    chatId,
                    "Спасибо за покупку 🙌\nСвяжитесь с 👉 @bapetaype",
                    replyMarkup: new InlineKeyboardMarkup(
                        InlineKeyboardButton.WithCallbackData("⬅️ Главное меню", "main_menu")
                    ),
                    cancellationToken: ct
                );
                break;
        }
    }

    static Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        Console.WriteLine(ex);
        return Task.CompletedTask;
    }
}
