using Core.Models;

namespace Core.Services;

public interface IAskManager
{
    Task AskAsync(long telegramId, string text);
    Task<Asking?> GetUserCurrentReplyAskAsync(long telegramUserId);
    Task<Asking?> GetUserCurrentAskAsync(long telegramUserId);
    Task<Asking?> SelectRandomAskAsync(long telegramUserId);
    Task<(bool haveToNotify, long telegramUserId)> ReplyAskAsync(long telegramUserId, string text);
    Task<IEnumerable<Reply>> GetRepliesAsync(long telegramUserId);
}
