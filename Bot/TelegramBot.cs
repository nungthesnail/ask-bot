using Core.Models;
using Core.Services.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Mapster;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using User = Core.Models.User;

namespace Bot;

public sealed class TelegramBot(TelegramBotClient botClient, IServiceScopeFactory scopeFactory)
    : ITelegramBot
{
    private readonly CancellationTokenSource _cts = new();

    public async Task StartAsync()
    {
        var me = await botClient.GetMe();
        Log.Information("Bot id: {id}, name: {name}.", me.Id, me.FirstName);
        botClient.OnMessage += BotOnMessageReceived;
    }

    private async Task BotOnMessageReceived(Message message, UpdateType type)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var userStorage = scope.ServiceProvider.GetRequiredService<IUserStorage>();
        var controller = scope.ServiceProvider.GetRequiredService<IActionController>();
        
        try
        {
            var user = userStorage.GetOrCreateUser(message.Chat.Id);
            if (!IsMessageValid(message))
            {
                await controller.SendOnlyTextAllowedAsync(message.Adapt<MessageDto>());
                return;
            }
            
            if (IsResetCommand(message))
            {
                await controller.ResetUserStateAsync(user, sendHelloMessage: true);
                return;
            }

            if (IsInfoCommand(message))
            {
                await controller.SendInfoMessageAsync(user);
                return;
            }

            await HandleUserState(message, user, controller);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing message");
            await controller.SendFaultMessageAsync(message.Adapt<MessageDto>());
        }
    }

    private static bool IsMessageValid(Message message)
        => message is { Type: MessageType.Text, Text: not null };
    private static bool IsInfoCommand(Message message)
        => message.Text is not null
           && message.Text.StartsWith("/info", StringComparison.Ordinal);
    private static bool IsResetCommand(Message message)
        => message.Text is not null
           && (message.Text.StartsWith("/stop", StringComparison.Ordinal)
           || message.Text.StartsWith("/start", StringComparison.Ordinal));

    private static Task HandleUserState(Message message, User user, IActionController controller)
    {
        return user.State switch
        {
            UserState.Waiting => HandleWaitingState(message, user, controller),
            UserState.InputtingQuestion => controller.HandleQuestionInputAsync(message.Adapt<MessageDto>(), user),
            UserState.InputtingAnswer => controller.HandleAnswerInputAsync(message.Adapt<MessageDto>(), user),
            UserState.WaitingForAnswers => HandleWaitingForAnswers(message, user, controller),
            _ => Task.CompletedTask
        };
    }

    private static async Task HandleWaitingState(Message message, User user, IActionController controller)
    {
        if (message.Text is null)
        {
            await controller.SendFaultMessageAsync(message.Adapt<MessageDto>());
            return;
        }

        if (message.Text.StartsWith("/ask", StringComparison.Ordinal))
        {
            await controller.StartAskAsync(message.Adapt<MessageDto>(), user);
        }
        else if (message.Text.StartsWith("/answer", StringComparison.Ordinal))
        {
            await controller.StartAnswerAsync(message.Adapt<MessageDto>(), user);
        }
    }

    private static async Task HandleWaitingForAnswers(Message message, User user, IActionController controller)
    {
        if (message.Text is null)
        {
            await controller.SendFaultMessageAsync(message.Adapt<MessageDto>());
            return;
        }

        await controller.HelpToStopAskAsync(user);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    ~TelegramBot()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
