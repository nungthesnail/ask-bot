using Bot;
using Core.Services.Implementations;
using Microsoft.Extensions.Configuration;

var questionStorage = new QuestionStorage();
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();
await using var bot = new TelegramBot(questionStorage, configuration);
await bot.StartAsync();

Console.WriteLine("Press key C for stop...");
while (true)
{
    var key = Console.ReadKey();
    if (key.Key == ConsoleKey.C)
        break;
}
