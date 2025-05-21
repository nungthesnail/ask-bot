namespace Core.Models;

public class User
{
    public int Id { get; set; }
    public long TelegramId { get; set; }
    public int? CurrentReplyingAskingId { get; set; }
    
    public List<Asking>? Askings { get; set; }
    public List<Reply>? Replies { get; set; }
}
