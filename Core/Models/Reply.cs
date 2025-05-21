using System.ComponentModel.DataAnnotations;

namespace Core.Models;

public class Reply
{
    public const int MaxTextLength = 500;
    
    public int Id { get; set; }
    public User? User { get; set; }
    public int UserId { get; set; }
    public Asking? Asking { get; set; }
    public int AskingId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    [MaxLength(500)]
    public required string Text { get; set; }
}
