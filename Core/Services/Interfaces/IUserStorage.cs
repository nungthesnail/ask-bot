using Core.Models;

namespace Core.Services.Interfaces;

public interface IUserStorage
{
    User GetOrCreateUser(long chatId);
    bool TryGetUser(long chatId, out User? user);
    int Count { get; }
}