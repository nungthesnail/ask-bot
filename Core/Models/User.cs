﻿namespace Core.Models;

public class User
{
    public long ChatId { get; set; }
    public UserState State { get; set; }
    public long? QuestionId { get; set; }
    public long? AnswerToChatId { get; set; }
    public int TokenCount { get; set; } = 1;
    
    #region Cached data
    
    public DateTimeOffset? CachedAnswerQuestionExpiry { get; set; }
    public long? CachedAnswerToQuestionId { get; set; }
    
    #endregion
}
