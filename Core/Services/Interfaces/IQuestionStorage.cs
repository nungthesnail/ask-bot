using Core.Models;

namespace Core.Services.Interfaces;

public interface IQuestionStorage
{
    long AddQuestion(string text, long askedBy);
    Question? GetRandomQuestion();
    void DeleteQuestion(long questionId);
    int CountQuestions();
}
