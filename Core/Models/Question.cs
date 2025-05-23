namespace Core.Models;

public class Question
{
    public long Id { get; set; }
    public required string Text { get; set; }
    public long AskedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}
