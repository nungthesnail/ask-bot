using System.Collections.Concurrent;
using Core.Models;
using Core.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Configuration;
using User = Core.Models.User;

namespace Bot;

public class TelegramBot : ITelegramBot
{
    private readonly TelegramBotClient _botClient;
    private readonly ConcurrentDictionary<long, User> _users = [];
    private readonly IQuestionStorage _questionStorage;

    private bool _isTesting;
    
    public CancellationTokenSource CancellationTokenSource { get; } = new();

    public TelegramBot(IQuestionStorage questionStorage, IConfiguration config)
    {
        var token = config["BotToken"] ?? throw new ConfigurationErrorsException("BotToken is missing");
        _botClient = new TelegramBotClient(token, cancellationToken: CancellationTokenSource.Token);
        _questionStorage = questionStorage;
        _isTesting = config.GetValue("IsTesting", defaultValue: false);
    }

    public async Task StartAsync()
    {
        var me = await _botClient.GetMe();
        Console.WriteLine($"Bot id: {me.Id}, name: {me.FirstName}. Testing: {_isTesting}");
        _botClient.OnMessage += BotOnMessageReceived;
    }

    private async Task BotOnMessageReceived(Message message, UpdateType updateType)
    {
        var chatId = message.Chat.Id;

        if (!_users.ContainsKey(chatId))
        {
            var newUser = new User { ChatId = chatId, State = UserState.Waiting };
            _users.TryAdd(chatId, newUser);
            await _botClient.SendMessage(chatId,
                "Добро пожаловать! Используйте /ask для задания вопроса или /answer для ответа на вопрос.");
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
            await _botClient.SendMessage(user.ChatId, "Введите ваш вопрос:");
        }
        else if (message.Text.StartsWith("/answer", StringComparison.Ordinal))
        {
            var question = _questionStorage.GetRandomQuestion();
            if (question is not null)
            {
                await _botClient.SendMessage(user.ChatId, $"Вопрос: {question.Text}");
                user.State = UserState.InputtingAnswer;
                user.AnswerToChatId = question.AskedBy;
            }
            else
            {
                await _botClient.SendMessage(user.ChatId, "Нет доступных вопросов для ответа :(");
            }
        }
    }

    private async Task SendFaultMessage(Message message)
    {
        await _botClient.SendMessage(message.Chat.Id, "Что-то пошло не так :(\nПопробуйте снова");
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
        await _botClient.SendMessage(user.ChatId,
            "Ваш вопрос успешно задан! Ожидайте, когда кто-нибудь ответит на него");
        Console.WriteLine("A question created: {0}", message.Text);
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
            await _botClient.SendMessage(user.AnswerToChatId.Value, message.Text);
            user.State = UserState.Waiting;
            await _botClient.SendMessage(user.ChatId, "Ваш ответ отправлен!");
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
            user.QuestionId = null;
            await _botClient.SendMessage(user.ChatId, "Вы закончили принимать ответы на вопрос");
        }
        else
        {
            await _botClient.SendMessage(user.ChatId, "Чтобы прекратить принимать ответы на вопрос, введите /stop");
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        await CancellationTokenSource.CancelAsync();
        CancellationTokenSource.Dispose();
        GC.SuppressFinalize(this);
    }
}
