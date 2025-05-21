using Core.Models;
using Core.Services;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.Implementations;

public class EfUserManager(AppDbContext dbContext) : IUserManager
{
    public async Task AddUserAsync(User user)
    {
        await dbContext.Users.AddAsync(user);
    }

    public Task<User?> GetUserAsync(long telegramId)
    {
        return dbContext.Users.Where(x => x.TelegramId == telegramId).FirstOrDefaultAsync();
    }
}
