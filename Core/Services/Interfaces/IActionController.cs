using Core.Models;

namespace Core.Services.Interfaces;

public interface IActionController
{
    Task ResetUserStateAsync(User user, bool sendHelloMessage = false);
    Task StartAskAsync(MessageDto message, User user);
    Task StartAnswerAsync(MessageDto message, User user);
    Task SendFaultMessageAsync(MessageDto message);
    Task HandleQuestionInputAsync(MessageDto message, User user);
    Task HandleAnswerInputAsync(MessageDto message, User user);
    Task StopAnswerWaitingAsync(MessageDto message, User user);
    Task HelpToStopAskAsync(User user);

    Task SendHelloMessageAsync(User user);
    Task SendInfoMessageAsync(User user);
    Task SendOnlyTextAllowedAsync(MessageDto message);
}
