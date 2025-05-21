using System.ComponentModel.DataAnnotations;

namespace Core.Models;

public class Asking
{
    public const int MaxTextLength = 500;
    
    public int Id { get; set; }
    public User? User { get; set; }
    public int UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int NeedRepliesCount { get; set; } = 1;
    [MaxLength(MaxTextLength)]
    public required string Text { get; set; }
    public bool Active { get; set; }
    
    public List<Reply>? Replies { get; set; }
}
