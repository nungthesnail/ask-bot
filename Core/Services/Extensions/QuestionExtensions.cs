using Core.Models;

namespace Core.Services.Extensions;

public static class QuestionExtensions
{
    private const int LifetimeMinutes = 10;
    
    public static bool HaveToDelete(this Question question)
        => DateTimeOffset.Now - question.CreatedAt > TimeSpan.FromMinutes(LifetimeMinutes);
}
