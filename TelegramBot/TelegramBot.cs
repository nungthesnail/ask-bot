using System.Collections.Concurrent;
using System.Text;
using Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using User = Core.Models.User;

namespace TelegramBot;

public class TelegramBot
{
    private readonly TelegramBotClient _client;
    private readonly IServiceProvider _services;
    private readonly ConcurrentDictionary<long, UserState> _userStates = [];
    
    public TelegramBot(TelegramBotClient botClient, IServiceProvider serviceProvider)
    {
        _client = botClient;
        _services = serviceProvider;
        
        botClient.OnMessage += OnMessageReceived;
    }

    private async Task OnMessageReceived(Message message, UpdateType updateType)
    {
        var scopeFactory = _services.GetRequiredService<IServiceScopeFactory>();
        await using var scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<IUserManager>();
        
        // Checking user
        var chatId = message.Chat.Id;
        var state = _userStates.GetOrAdd(chatId, static _ => UserState.Idle);
        var user = await userManager.GetUserAsync(chatId);
        if (user is null)
        {
            user = new User
            {
                TelegramId = chatId
            };
            await userManager.AddUserAsync(user);
        }

        // Dispatching message
        switch (state)
        {
            case UserState.WaitAsking:
                await DispatchAskingInput(message, scope.ServiceProvider);
                break;
            case UserState.WaitReply:
                await DispatchReplyInput(message, scope.ServiceProvider);
                break;
            default:
                await DispatchIdleMessage(message, user, scope.ServiceProvider);
                break;
        }
    }
    
    private async Task DispatchAskingInput(Message message, IServiceProvider services)
    {
        try
        {
            var askManager = services.GetRequiredService<IAskManager>();
            var text = message.Text ?? throw new InvalidOperationException("Text is null");
            await askManager.AskAsync(message.Chat.Id, text);
            _userStates[message.Chat.Id] = UserState.Idle;
        }
        catch (Exception)
        {
            await _client.SendMessage(
                chatId: message.Chat.Id,
                text: "Что-то пошло не так :(");
        }
    }

    private async Task DispatchReplyInput(Message message, IServiceProvider services)
    {
        try
        {
            var askManager = services.GetRequiredService<IAskManager>();
            var text = message.Text ?? throw new InvalidOperationException("Text is null");
            var result = await askManager.ReplyAskAsync(message.Chat.Id, text);
            _userStates[message.Chat.Id] = UserState.Idle;
            if (!result.haveToNotify)
                return;

            var replies = await askManager.GetRepliesAsync(result.telegramUserId);
            var reply = new StringBuilder();
            reply.AppendLine("Вам ответили!");
            foreach (var r in replies)
            {
                reply.AppendLine("----------");
                reply.AppendLine(r.Text);
            }
        }
        catch (Exception)
        {
            await _client.SendMessage(
                chatId: message.Chat.Id,
                text: "Что-то пошло не так :(");
        }
    }
    
    private Task DispatchIdleMessage(Message message, User user, IServiceProvider services)
    {
        throw new NotImplementedException();
    }
}
