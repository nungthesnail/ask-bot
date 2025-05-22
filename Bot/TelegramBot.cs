using System.Collections.Concurrent;
using Core.Models;
using Core.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Configuration;
using Serilog;
using Telegram.Bot.Types.ReplyMarkups;
using User = Core.Models.User;

namespace Bot;

public sealed class TelegramBot : ITelegramBot
{
    private readonly TelegramBotClient _botClient;
    private readonly ConcurrentDictionary<long, User> _users = [];
    private readonly IQuestionStorage _questionStorage;
    private readonly IResourceManager _resourceManager;
    
    private readonly CancellationTokenSource _cts = new();

    public TelegramBot(IQuestionStorage questionStorage, IResourceManager resourceManager, IConfiguration config)
    {
        var token = config["BotToken"] ?? throw new ConfigurationErrorsException("BotToken is missing");
        _botClient = new TelegramBotClient(token, cancellationToken: _cts.Token);
        _questionStorage = questionStorage;
        _resourceManager = resourceManager;
    }

    public async Task StartAsync()
    {
        var me = await _botClient.GetMe();
        Log.Information("Bot id: {id}, name: {name}.", me.Id, me.FirstName);
        _botClient.OnMessage += BotOnMessageReceived;
    }

    private async Task BotOnMessageReceived(Message message, UpdateType updateType)
    {
        var err = await ValidateMessage(message);
        if (err)
            return;
        var chatId = message.Chat.Id;

        try
        {
            User? user;
            if (!_users.TryGetValue(chatId, out user))
            {
                user = new User
                {
                    ChatId = chatId,
                    State = UserState.Waiting
                };
                _users.TryAdd(chatId, user);
            }

            if (HaveToSendHello(message))
            {
                var helpMessage = _resourceManager.Get(TextRes.Hello, user.TokenCount);

                var keyboard = new ReplyKeyboardMarkup([
                    ["/ask", "/answer"],
                    ["/start"],
                    ["/stop"]
                ])
                {
                    ResizeKeyboard = true
                };
                await _botClient.SendMessage(chatId, helpMessage, replyMarkup: keyboard);
                return;
            }
            if (HaveToSendInfo(message))
            {
                await _botClient.SendMessage(user.ChatId, _resourceManager.Get(
                    TextRes.Info, user.TokenCount, user.ChatId));
                return;
            }
            if (HaveToResetState(message))
            {
                user.State = UserState.Waiting;
                await _botClient.SendMessage(chatId, _resourceManager.Get(TextRes.Hello, user.TokenCount));
                return;
            }
            
            switch (user.State)
            {
                case UserState.Waiting:
                    await HandleWaitingState(message, user);
                    break;

                case UserState.InputtingQuestion:
                    await HandleInputtingQuestion(message, user);
                    break;

                case UserState.InputtingAnswer:
                    await HandleInputtingAnswer(message, user);
                    break;

                case UserState.WaitingForAnswers:
                    await HandleWaitingAnswers(message, user);
                    break;
            }
        }
        catch (Exception exc)
        {
            Log.Error("Something failed: {exc}", exc);
            await SendFaultMessage(message);
        }
    }
    
    private async Task<bool> ValidateMessage(Message message)
    {
        if (message.Type == MessageType.Text && message.Text is not null) return false;
        await _botClient.SendMessage(message.Chat.Id, _resourceManager.Get(TextRes.OnlyTextAllowed));
        return true;
    }

    private static bool HaveToSendHello(Message message)
        => message.Text is not null && message.Text.StartsWith("/start", StringComparison.Ordinal);
    
    private bool HaveToSendInfo(Message message)
        => message.Text is not null && message.Text.StartsWith("/info", StringComparison.Ordinal);
    
    private bool HaveToResetState(Message message)
        => message.Text is not null && (message.Text.StartsWith("/stop", StringComparison.Ordinal) ||
                                        message.Text.StartsWith("/start", StringComparison.Ordinal));

    private async Task SendFaultMessage(Message message)
    {
        await _botClient.SendMessage(message.Chat.Id, _resourceManager.Get(TextRes.Fault));
    }

    private async Task HandleWaitingState(Message message, User user)
    {
        if (message.Text is null)
        {
            await SendFaultMessage(message);
            return;
        }

        // Обработка команды /ask и /answer
        if (message.Text.StartsWith("/ask", StringComparison.Ordinal))
        {
            // Checking that user have tokens
            if (user.TokenCount <= 0)
            {
                await _botClient.SendMessage(user.ChatId, _resourceManager.Get(TextRes.NoTokens));
                return;
            }
            user.State = UserState.InputtingQuestion;
            user.TokenCount--;
            await _botClient.SendMessage(user.ChatId, _resourceManager.Get(TextRes.InputQuestion));
        }
        else if (message.Text.StartsWith("/answer", StringComparison.Ordinal))
        {
            var question = _questionStorage.GetRandomQuestion();
            if (question is not null)
            {
                await _botClient.SendMessage(user.ChatId, _resourceManager.Get(TextRes.Question, question.Text));
                user.State = UserState.InputtingAnswer;
                user.AnswerToChatId = question.AskedBy;
            }
            else
            {
                await _botClient.SendMessage(user.ChatId, _resourceManager.Get(TextRes.NoQuestions));
                if (user.TokenCount <= 0)
                {
                    user.TokenCount++;
                    await _botClient.SendMessage(user.ChatId, _resourceManager.Get(TextRes.GiftToken));
                }
            }
        }
    }

    private async Task HandleInputtingQuestion(Message message, User user)
    {
        if (message.Text is null)
        {
            await SendFaultMessage(message);
            return;
        }
        
        var questionId = _questionStorage.AddQuestion(message.Text, user.ChatId);
        user.State = UserState.WaitingForAnswers;
        user.QuestionId = questionId;
        await _botClient.SendMessage(user.ChatId, _resourceManager.Get(TextRes.QuestionCreated));
        Log.Debug("User chat {chatId} created question {qId}: {text}", message.Chat.Id, questionId, message.Text);
    }

    private async Task HandleInputtingAnswer(Message message, User user)
    {
        if (message.Text is null)
        {
            await SendFaultMessage(message);
            return;
        }
        
        // Отправка ответа пользователю, который задал вопрос
        if (user.AnswerToChatId != null)
        {
            await _botClient.SendMessage(user.AnswerToChatId.Value,
                _resourceManager.Get(TextRes.Answer, message.Text));
            user.State = UserState.Waiting;
            user.TokenCount++;
            await _botClient.SendMessage(user.ChatId, _resourceManager.Get(TextRes.AnswerSent));
        }
    }

    private async Task HandleWaitingAnswers(Message message, User user)
    {
        if (message.Text is null)
        {
            await SendFaultMessage(message);
            return;
        }

        if (message.Text.StartsWith("/stop", StringComparison.Ordinal))
        {
            user.State = UserState.Waiting;
            if (user.QuestionId is not null)
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
