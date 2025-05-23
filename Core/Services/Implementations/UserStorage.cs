using System.Collections.Concurrent;
using Core.Models;
using Core.Services.Interfaces;

namespace Core.Services.Implementations;

public class UserStorage : IUserStorage
{
    private readonly ConcurrentDictionary<long, User> _users = new();

    public User GetOrCreateUser(long chatId)
    {
        return _users.GetOrAdd(chatId, static id => new User
        {
            ChatId = id,
            State = UserState.Waiting
        });
    }

    public bool TryGetUser(long chatId, out User? user)
    {
        return _users.TryGetValue(chatId, out user);
    }

    public int Count => _users.Count;
}
