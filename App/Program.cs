using System.Collections.ObjectModel;
using System.Configuration;
using Bot;
using Core.Services.Implementations;
using Microsoft.Extensions.Configuration;
using Serilog;

// Configuration and logging
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

// Dependencies
var resources = configuration.GetSection("Resources").Get<Dictionary<string, string>>()
                ?? throw new ConfigurationErrorsException("Resources is missing");
var resourceManager = new ResourceManager(new ReadOnlyDictionary<string, string>(resources));
var questionStorage = new QuestionStorage();
var userStorage = new UserStorage();

// Start
await using var bot = new TelegramBot(
    questionStorage: questionStorage,
    userStorage: userStorage,
    resourceManager: resourceManager,
    config: configuration);
await bot.StartAsync();

Log.Information("Press key C for stop...");
while (true)
{
    var key = Console.Read();
    if (key == 'c')
        break;
}
