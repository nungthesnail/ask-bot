using Core.Models;
using Core.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Configuration;
using Core.Services.Extensions;
using Serilog;
using Telegram.Bot.Types.ReplyMarkups;
using User = Core.Models.User;

namespace Bot;

public sealed class TelegramBot : ITelegramBot
{
    private readonly TelegramBotClient _botClient;
    private readonly IUserStorage _userStorage;
    private readonly IQuestionStorage _questionStorage;
    private readonly IResourceManager _resourceManager;
    private readonly CancellationTokenSource _cts = new();

    public TelegramBot(
        IQuestionStorage questionStorage,
        IUserStorage userStorage,
        IResourceManager resourceManager,
        IConfiguration config)
    {
        var token = config["BotToken"] ?? throw new ConfigurationErrorsException("BotToken is missing");
        _botClient = new TelegramBotClient(token, cancellationToken: _cts.Token);
        _questionStorage = questionStorage;
        _resourceManager = resourceManager;
        _userStorage = userStorage;
    }

    public async Task StartAsync()
    {
        var me = await _botClient.GetMe();
        Log.Information("Bot id: {id}, name: {name}.", me.Id, me.FirstName);
        _botClient.OnMessage += BotOnMessageReceived;
    }

    private async Task BotOnMessageReceived(Message message, UpdateType type)
    {
        try
        {
            var user = _userStorage.GetOrCreateUser(message.Chat.Id);
            if (await ValidateMessage(message))
            {
                return;
            }

            if (IsStartCommand(message))
            {
                await SendWelcomeMessage(user);
                return;
            }

            if (IsInfoCommand(message))
            {
                await SendInfoMessage(user);
                return;
            }

            if (IsResetCommand(message))
            {
                await ResetUserState(user);
                return;
            }

            await HandleUserState(message, user);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing message");
            await SendFaultMessage(message);
        }
    }

    private async Task<bool> ValidateMessage(Message message)
    {
        if (message is { Type: MessageType.Text, Text: not null })
            return false;

        await _botClient.SendMessage(message.Chat.Id, _resourceManager.Get(TextRes.OnlyTextAllowed));
        return true;
    }

    private bool IsStartCommand(Message message)
        => message.Text is not null
           && message.Text.StartsWith("/start", StringComparison.Ordinal);
    private bool IsInfoCommand(Message message)
        => message.Text is not null
           && message.Text.StartsWith("/info", StringComparison.Ordinal);
    private bool IsResetCommand(Message message)
        => message.Text is not null
           && (message.Text.StartsWith("/stop", StringComparison.Ordinal)
           || message.Text.StartsWith("/start", StringComparison.Ordinal));

    private async Task SendWelcomeMessage(User user)
    {
        var helpMessage = _resourceManager.Get(TextRes.Hello, user.TokenCount);
        var keyboard = new ReplyKeyboardMarkup([
            [ "/ask", "/answer" ],
            [ "/start" ],
            [ "/stop" ]
        ])
        {
            ResizeKeyboard = true
        };
        await _botClient.SendMessage(user.ChatId, helpMessage, replyMarkup: keyboard);
    }

    private async Task SendInfoMessage(User user)
    {
        await _botClient.SendMessage(user.ChatId, _resourceManager.Get(
            TextRes.Info, user.TokenCount, user.ChatId, _userStorage.Count, _questionStorage.CountQuestions()));
    }

    private async Task ResetUserState(User user)
    {
        user.State = UserState.Waiting;
        await _botClient.SendMessage(user.ChatId, _resourceManager.Get(TextRes.Hello, user.TokenCount));
        if (user.QuestionId is not null)
            _questionStorage.DeleteQuestion(user.QuestionId.Value);
        if (user.AnswerToChatId is not null)
            user.AnswerToChatId = user.AnswerToChatId.Value;
    }

    private Task HandleUserState(Message message, User user)
    {
        return user.State switch
        {
            UserState.Waiting => HandleWaitingState(message, user),
            UserState.InputtingQuestion => HandleInputQuestion(message, user),
            UserState.InputtingAnswer => HandleInputAnswer(message, user),
            UserState.WaitingForAnswers => HandleWaitingForAnswers(message, user),
            _ => Task.CompletedTask
        };
    }

    private async Task HandleWaitingState(Message message, User user)
    {
        if (message.Text == null) { await SendFaultMessage(message); return; }

        if (message.Text.StartsWith("/ask", StringComparison.Ordinal))
        {
            await AskQuestion(user);
        }
        else if (message.Text.StartsWith("/answer", StringComparison.Ordinal))
        {
            await AnswerQuestion(user);
        }
    }

    private async Task AskQuestion(User user)
    {
        if (!user.CanCreateQuestion())
        {
            await _botClient.SendMessage(user.ChatId, _resourceManager.Get(TextRes.NoTokens));
            return;
        }
        user.State = UserState.InputtingQuestion;
        user.PayQuestionCreation();
        await _botClient.SendMessage(user.ChatId, _resourceManager.Get(TextRes.InputQuestion));
    }

    private async Task AnswerQuestion(User user)
    {
        var question = _questionStorage.GetRandomQuestion();
        if (question != null)
        {
            await _botClient.SendMessage(user.ChatId, _resourceManager.Get(TextRes.Question, question.Text));
            user.State = UserState.InputtingAnswer;
            user.AnswerToChatId = question.AskedBy;
        }
        else
        {
            await _botClient.SendMessage(user.ChatId, _resourceManager.Get(TextRes.NoQuestions));
            if (!user.CanCreateQuestion())
            {
                user.GiftTokensToCreateQuestion();
                await _botClient.SendMessage(user.ChatId, _resourceManager.Get(TextRes.GiftToken));
            }
        }
    }

    private async Task HandleInputQuestion(Message message, User user)
    {
        if (message.Text == null) { await SendFaultMessage(message); return; }
        var questionId = _questionStorage.AddQuestion(message.Text, user.ChatId);
        user.State = UserState.WaitingForAnswers;
        user.QuestionId = questionId;
        await _botClient.SendMessage(user.ChatId, _resourceManager.Get(TextRes.QuestionCreated));
        Log.Debug("User chat {chatId} created question {qId}: {text}", message.Chat.Id, questionId, message.Text);
    }

    private async Task HandleInputAnswer(Message message, User user)
    {
        // Checks
        if (message.Text is null)
        {
            await SendFaultMessage(message);
            return;
        }
        if (user.AnswerToChatId is null)
            return;
        
        // Sending notifications
        await _botClient.SendMessage(user.AnswerToChatId.Value, _resourceManager.Get(TextRes.Answer, message.Text));
        user.State = UserState.Waiting;
        user.GiftTokensForAnswer();
        await _botClient.SendMessage(user.ChatId, _resourceManager.Get(TextRes.AnswerSent));

        // Deleting question
        var question = _questionStorage.GetQuestionByChatId(user.AnswerToChatId.Value);
        if (question is null || !question.HaveToDelete())
            return;
        _questionStorage.DeleteQuestion(user.AnswerToChatId.Value);
        await _botClient.SendMessage(user.AnswerToChatId, _resourceManager.Get(TextRes.QuestionExpired));
    }

    private async Task HandleWaitingForAnswers(Message message, User user)
    {
        if (message.Text == null) { await SendFaultMessage(message); return; }
        if (message.Text.StartsWith("/stop", StringComparison.Ordinal))
        {
            user.State = UserState.Waiting;
            if (user.QuestionId != null)
            {
                _questionStorage.DeleteQuestion(user.QuestionId.Value);
            }
            await _botClient.SendMessage(user.ChatId, _resourceManager.Get(TextRes.QuestionStopped));
            Log.Debug("User chat {id} stopped question {qId}", message.Chat.Id, user.QuestionId);
            user.QuestionId = null;
        }
        else
        {
            await _botClient.SendMessage(user.ChatId, _resourceManager.Get(TextRes.QuestionStopHelp));
        }
    }

    private async Task SendFaultMessage(Message message)
    {
        await _botClient.SendMessage(message.Chat.Id, _resourceManager.Get(TextRes.Fault));
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
