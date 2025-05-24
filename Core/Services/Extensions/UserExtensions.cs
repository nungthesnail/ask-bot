using Core.Models;

namespace Core.Services.Extensions;

public static class UserExtensions
{
    private const int QuestionPrice = 10;
    private const int RewardAmount = 1;
    
    public static void PayQuestionCreation(this User user)
    {
        if (!user.CanCreateQuestion())
            throw new InvalidOperationException("Not enough tokens to create a question");
        user.TokenCount -= QuestionPrice;
    }

    public static bool CanCreateQuestion(this User user)
        => user.TokenCount >= QuestionPrice;
    public static void GiftTokensToCreateQuestion(this User user)
        => user.TokenCount = user.TokenCount >= QuestionPrice ? user.TokenCount : QuestionPrice;
    public static void GiftTokensForAnswer(this User user)
        => user.TokenCount += RewardAmount;
}
