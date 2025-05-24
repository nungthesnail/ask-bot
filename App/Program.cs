using System.Collections.ObjectModel;
using System.Configuration;
using Bot;
using Core.Models;
using Core.Services.Implementations;
using Core.Services.Interfaces;
using Mapster;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Types;

var appServices = PrepareApp();
await using var bot = appServices.GetRequiredService<ITelegramBot>();
await bot.StartAsync();

Log.Information("Press key C for stop...");
while (true)
{
    var key = Console.Read();
    if (key is 'c' or 'C')
        break;
}

Log.Information("Stopping...");
return;

static IServiceProvider PrepareApp()
{
    // Libraries
    TypeAdapterConfig<Message, MessageDto>.NewConfig()
        .Map(static x => x.Text, static y => y.Text)
        .Map(static x => x.ChatId, static x => x.Chat.Id);

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

    // Telegram bot client
    var botClient = new TelegramBotClient(token: configuration["BotToken"]
                                                 ?? throw new ConfigurationErrorsException("Bot token is missing"));

    // Dependencies
    var resources = configuration.GetSection("Resources").Get<Dictionary<string, string>>()
                    ?? throw new ConfigurationErrorsException("Resources is missing");
    var resourceManager = new ResourceManager(new ReadOnlyDictionary<string, string>(resources));
    var questionStorage = new QuestionStorage();
    var userStorage = new UserStorage();
    
    return new ServiceCollection()
        .AddSingleton<IResourceManager>(resourceManager)
        .AddSingleton<IUserStorage>(userStorage)
        .AddSingleton<IQuestionStorage>(questionStorage)
        .AddSingleton<ITelegramBot, TelegramBot>()
        .AddScoped<IActionController, ActionController>()
        .AddTransient<IBotAdapter, TelegramBotAdapter>()
        .AddSingleton(botClient)
        .BuildServiceProvider();

}
