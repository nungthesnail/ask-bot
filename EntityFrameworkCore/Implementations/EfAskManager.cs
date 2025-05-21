using Core.Models;
using Core.Services;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.Implementations;

public class EfAskManager(AppDbContext dbContext) : IAskManager
{
    public async Task AskAsync(long telegramId, string text)
    {
        var user = await dbContext.Users.Where(x => x.TelegramId == telegramId).FirstOrDefaultAsync();
        if (user is null)
            throw new InvalidOperationException("User not found");
        if (user.CurrentReplyingAskingId is not null)
            throw new InvalidOperationException("User already created asking");
        var asking = new Asking
        {
            UserId = user.Id,
            CreatedAt = DateTimeOffset.Now,
            Active = true,
            Text = text,
            NeedRepliesCount = 1
        };
        await dbContext.Askings.AddAsync(asking);
        await dbContext.SaveChangesAsync();
    }

    public async Task<Asking?> GetUserCurrentReplyAskAsync(long telegramUserId)
    {
        var id = dbContext.Users
            .Where(x => x.TelegramId == telegramUserId)
            .Select(static x => x.CurrentReplyingAskingId)
            .FirstOrDefault();
        if (id is null)
            return null;
        return await dbContext.Askings.FindAsync(id);
    }

    public async Task<Asking?> GetUserCurrentAskAsync(long telegramUserId)
    {
        var userIds = await dbContext.Users
            .Where(x => x.TelegramId == telegramUserId)
            .Select(static x => x.Id)
            .ToListAsync();
        if (userIds.Count < 1)
            throw new InvalidOperationException("User not found");
        var userId = userIds.First();
        
        var asking = await dbContext.Askings.Where(x => x.UserId == userId && x.Active).FirstOrDefaultAsync();
        return asking;
    }

    public async Task<Asking?> SelectRandomAskAsync(long telegramUserId)
    {
        var replyAsking = await GetUserCurrentReplyAskAsync(telegramUserId);
        if (replyAsking is not null)
            throw new InvalidOperationException("User is already replying asking");
        var user = await dbContext.Users.Where(x => x.TelegramId == telegramUserId).FirstOrDefaultAsync();
        if (user is null)
            return null;
        var asking = await dbContext.Askings
            .Include(static x => x.Replies)
            .Where(x => x.UserId != user.Id && x.Replies!.Any(y => y.UserId != user.Id) && x.Active)
            .FirstOrDefaultAsync();
        return asking;
    }

    public async Task<(bool haveToNotify, long telegramUserId)> ReplyAskAsync(long telegramUserId, string text)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        
        // Reset user current reply
        var user = await dbContext.Users.Where(x => x.TelegramId == telegramUserId).FirstOrDefaultAsync();
        if (user is null)
            throw new InvalidOperationException("User not found");
        if (user.CurrentReplyingAskingId is null)
            throw new InvalidOperationException("User is not replying an asking");
        
        // Checking asking and adding reply
        var reply = new Reply
        {
            UserId = user.Id,
            AskingId = user.CurrentReplyingAskingId.Value,
            Text = text,
            CreatedAt = DateTimeOffset.Now,
        };
        var asking = await dbContext.Askings.FindAsync(reply.AskingId);
        if (asking is null)
            throw new InvalidOperationException("Asking not found");
        await dbContext.AddAsync(text);
        user.CurrentReplyingAskingId = null;
        dbContext.Update(user);
        
        // Counting the replies and inactivate asking if there are enough replies.
        var count = dbContext.Replies.Count(x => x.AskingId == reply.AskingId);
        if (count < asking.NeedRepliesCount)
        {
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            return (false, default);
        }

        asking.Active = false;
        dbContext.Askings.Update(asking);
        await dbContext.SaveChangesAsync();
        return (true, user.TelegramId);
    }

    public async Task<IEnumerable<Reply>> GetRepliesAsync(long telegramUserId)
    {
        var user = await dbContext.Users.Where(x => x.TelegramId == telegramUserId).FirstOrDefaultAsync();
        if (user is null)
            throw new InvalidOperationException("User not found");
        if (user.CurrentReplyingAskingId is null)
            throw new InvalidOperationException("User is not replying an asking");
        return await dbContext.Replies.Where(x => x.AskingId == user.CurrentReplyingAskingId).ToListAsync();
    }
}
