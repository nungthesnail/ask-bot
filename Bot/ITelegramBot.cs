namespace Bot;

public interface ITelegramBot : IAsyncDisposable
{
    Task StartAsync();
}
