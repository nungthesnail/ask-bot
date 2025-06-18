using Core.Models;
using Core.Services.Extensions;
using Core.Services.Interfaces;
using Serilog;

namespace Core.Services.Implementations;

public class ActionController(
    IUserStorage userStorage, IQuestionStorage questionStorage,
    IResourceManager resourceManager, IBotAdapter botAdapter)
    : IActionController
{
    public async Task ResetUserStateAsync(User user, bool sendHelloMessage = true)
    {
        user.State = UserState.Waiting;
        if (user.QuestionId is not null)
        {
            questionStorage.DeleteQuestion(user.QuestionId.Value);
            await botAdapter.SendMessageAsync(user.ChatId, resourceManager.Get(TextRes.QuestionStopped));
        }

        if (user.AnswerToChatId is not null)
            user.AnswerToChatId = user.AnswerToChatId.Value;

        if (sendHelloMessage)
            await SendHelloMessageAsync(user);
    }

    public async Task StartAskAsync(MessageDto message, User user)
    {
        if (!user.CanCreateQuestion())
        {
            await botAdapter.SendMessageAsync(user.ChatId, resourceManager.Get(TextRes.NoTokens));
            return;
        }
        user.State = UserState.InputtingQuestion;
        user.PayQuestionCreation();
        await botAdapter.SendMessageAsync(user.ChatId, resourceManager.Get(TextRes.InputQuestion));
    }

    public async Task StartAnswerAsync(MessageDto message, User user)
    {
        var question = questionStorage.GetRandomQuestion();
        if (question is not null)
        {
            await botAdapter.SendMessageAsync(user.ChatId, resourceManager.Get(TextRes.Question, question.Text));
            user.State = UserState.InputtingAnswer;
            user.AnswerToChatId = question.AskedBy;
            user.CachedAnswerQuestionExpiry = question.ExpirationTime;
            user.CachedAnswerToQuestionId = question.Id;
        }
        else
        {
            await botAdapter.SendMessageAsync(user.ChatId, resourceManager.Get(TextRes.NoQuestions));
            if (!user.CanCreateQuestion())
            {
                user.GiftTokensToCreateQuestion();
                await botAdapter.SendMessageAsync(user.ChatId, resourceManager.Get(TextRes.GiftToken));
            }
        }
    }

    public Task SendFaultMessageAsync(MessageDto message)
    {
        return botAdapter.SendMessageAsync(message.ChatId, resourceManager.Get(TextRes.Fault));
    }

    public async Task HandleQuestionInputAsync(MessageDto message, User user)
    {
        if (message.Text == null)
        {
            await SendFaultMessageAsync(message);
            return;
        }
        var questionId = questionStorage.AddQuestion(message.Text, user.ChatId);
        user.State = UserState.WaitingForAnswers;
        user.QuestionId = questionId;
        await botAdapter.SendMessageAsync(user.ChatId, resourceManager.Get(TextRes.QuestionCreated));
        Log.Debug("User chat {chatId} created question {questionId}: {text}", message.ChatId, questionId, message.Text);
    }

    public async Task HandleAnswerInputAsync(MessageDto message, User user)
    {
        // Checking data
        if (message.Text is null || user.AnswerToChatId is null)
        {
            await SendFaultMessageAsync(message);
            return;
        }

        // Sending notifications
        await botAdapter.SendMessageAsync(user.AnswerToChatId.Value, resourceManager.Get(TextRes.Answer, message.Text));
        user.State = UserState.Waiting;
        user.GiftTokensForAnswer();
        await botAdapter.SendMessageAsync(user.ChatId, resourceManager.Get(TextRes.AnswerSent));
        Log.Debug("User {userId} answered question of chat {questionId}", user.ChatId, user.AnswerToChatId);

        // Checking question expiration and deleting it
        var questionExpired = DateTimeOffset.Now > user.CachedAnswerQuestionExpiry;
        if (questionExpired)
        {
            var askedUser = userStorage.GetOrCreateUser(user.AnswerToChatId.Value);
            if (askedUser.QuestionId is not null && askedUser.QuestionId == user.CachedAnswerToQuestionId)
            {
                await ResetUserStateAsync(askedUser, sendHelloMessage: false);
                Log.Debug("Question {id} was deleted by the system", user.QuestionId);
            }
        }

        // Reset answered user state
        await ResetUserStateAsync(user, sendHelloMessage: false);
    }

    public async Task StopAnswerWaitingAsync(MessageDto message, User user)
    {
        await ResetUserStateAsync(user, sendHelloMessage: false);
        Log.Debug("User chat {id} stopped question {qId}", message.ChatId, user.QuestionId);
    }

    public async Task HelpToStopAskAsync(User user)
    {
        await botAdapter.SendMessageAsync(user.ChatId, resourceManager.Get(TextRes.QuestionStopHelp));
    }

    public async Task SendHelloMessageAsync(User user)
    {
        var helpMessage = resourceManager.Get(TextRes.Hello, user.TokenCount);
        var keyboard = new List<List<string>>
        {
            new() { "/ask", "/answer" },
            new() { "/start" },
            new() { "/stop" }
        };

        await botAdapter.SendMessageAsync(user.ChatId, helpMessage, replyKeyboard: keyboard);
    }

    public Task SendInfoMessageAsync(User user)
    {
        return botAdapter.SendMessageAsync(user.ChatId, resourceManager.Get(
            TextRes.Info, user.TokenCount, user.ChatId, userStorage.Count, questionStorage.CountQuestions()));
    }

    public Task SendOnlyTextAllowedAsync(MessageDto message)
    {
        return botAdapter.SendMessageAsync(message.ChatId, resourceManager.Get(TextRes.OnlyTextAllowed));
    }
}
