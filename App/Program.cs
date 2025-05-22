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

// Start
await using var bot = new TelegramBot(questionStorage, resourceManager, configuration);
await bot.StartAsync();

Log.Information("Press key C for stop...");
while (true)
{
    var key = Console.ReadKey();
    if (key.Key == ConsoleKey.C)
        break;
}
