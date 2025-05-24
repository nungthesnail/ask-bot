using Core.Services.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot;

public class TelegramBotAdapter(TelegramBotClient telegramBot) : IBotAdapter
{
    public async Task SendMessageAsync(long chatId, string message,
        IEnumerable<IEnumerable<string>>? replyKeyboard = null)
    {
        ReplyMarkup? replyMarkup = null;
        if (replyKeyboard is not null)
        {
            replyMarkup = new ReplyKeyboardMarkup(
                replyKeyboard.Select(
                    static x => x.Select(
                        static y => new KeyboardButton(y))));
        }
        await telegramBot.SendMessage(chatId, message, replyMarkup: replyMarkup);
    }
}
