namespace Core.Services.Interfaces;

public interface IBotAdapter
{
    Task SendMessageAsync(long chatId, string message, IEnumerable<IEnumerable<string>>? replyKeyboard = null);
}
