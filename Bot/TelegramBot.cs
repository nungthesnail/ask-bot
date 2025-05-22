using System.Collections.Concurrent;
using Core.Models;
using Core.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Configuration;
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
        Console.WriteLine($"Bot id: {me.Id}, name: {me.FirstName}.");
        _botClient.OnMessage += BotOnMessageReceived;
    }

    private async Task BotOnMessageReceived(Message message, UpdateType updateType)
    {
        var chatId = message.Chat.Id;

        try
        {
            if (!_users.ContainsKey(chatId))
            {
                var newUser = new User
                {
                    ChatId = chatId,
                    State = UserState.Waiting
                };
                _users.TryAdd(chatId, newUser);
            }

            if (HaveToSendHello(message))
            {
                var helpMessage = _resourceManager.Get(TextRes.Hello);

                var keyboard = new ReplyKeyboardMarkup([
                    ["/ask", "/answer"],
                    ["/start"]
                ])
                {
                    ResizeKeyboard = true
                };
                await _botClient.SendMessage(chatId, helpMessage, replyMarkup: keyboard);
            }

            var user = _users[chatId];

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
            Console.WriteLine($"Something failed: {exc}");
            await SendFaultMessage(message);
        }
    }

    private static bool HaveToSendHello(Message message)
        => message.Text is not null && message.Text.StartsWith("/start", StringComparison.Ordinal);

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
            user.State = UserState.InputtingQuestion;
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
        Console.WriteLine("User chat {0} created a question {1}: {2}", message.Chat.Id, questionId, message.Text);
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
            Console.WriteLine("User chat {0} stopped question {1}", message.Chat.Id, user.QuestionId);
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
