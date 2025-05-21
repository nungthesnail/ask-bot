using Core.Models;

namespace Core.Services;

public interface IUserManager
{
    Task AddUserAsync(User user);
    Task<User?> GetUserAsync(long telegramId);
}
