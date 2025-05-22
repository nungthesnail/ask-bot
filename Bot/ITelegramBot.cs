namespace Bot;

public interface ITelegramBot : IAsyncDisposable
{
    CancellationTokenSource CancellationTokenSource { get; }
    Task StartAsync();
}
