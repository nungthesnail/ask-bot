using Core.Models;
using Core.Services.Interfaces;

namespace Core.Services.Implementations;

public class QuestionStorage : IQuestionStorage
{
    private readonly object _lock = new();
    private readonly List<Question> _questions = [];
    private long _nextId = 1;

    public long AddQuestion(string text, long askedBy)
    {
        lock (_lock)
        {
            var question = new Question
            {
                Id = Interlocked.Increment(ref _nextId),
                Text = text,
                AskedBy = askedBy
            };
            _questions.Add(question);
            return question.Id;
        }
    }

    public Question? GetRandomQuestion()
    {
        var random = new Random();
        lock (_lock)
        {
            return _questions.Count > 0
                ? _questions[random.Next(_questions.Count)]
                : null;
        }
    }

    public void DeleteQuestion(long questionId)
    {
        lock (_lock)
        {
            var question = _questions.FirstOrDefault(x => x.Id == questionId);
            if (question is null)
                return;
            _questions.Remove(question);
        }
    }
}
