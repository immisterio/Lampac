using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;

namespace LampacTgBot;

public static class Program
{
    private const string ModelName = "gpt-4o-mini";
    private const string SystemInstruction = "Ты Telegram-помощник по проекту Lampac. Отвечай только тем, что известно из контекста; будь краток; если вопрос не про Lampac — откажи.";

    public static async Task Main(string[] args)
    {
        string? telegramToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        string? openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrWhiteSpace(telegramToken))
        {
            Console.Error.WriteLine("Переменная TELEGRAM_BOT_TOKEN не задана.");
            return;
        }

        if (string.IsNullOrWhiteSpace(openAiKey))
        {
            Console.Error.WriteLine("Переменная OPENAI_API_KEY не задана.");
            return;
        }

        using var httpClient = Utils.CreateHttpClient();
        var projectContext = new ProjectContext(httpClient);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            Console.WriteLine("Получен сигнал остановки. Завершаем работу...");
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        ProjectSession session;
        try
        {
            session = await projectContext.LoadAsync(cts.Token);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Ошибка при загрузке контекста Lampac: {ex.Message}");
            return;
        }

        var openAiClient = new OpenAiClient(openAiKey, ModelName, SystemInstruction);
        ITelegramBotClient telegramClient = new TelegramBotClient(telegramToken);

        var bot = new TelegramBot(telegramClient, session, openAiClient);

        try
        {
            await bot.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Завершение по запросу.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Критическая ошибка: {ex}");
        }
    }
}
