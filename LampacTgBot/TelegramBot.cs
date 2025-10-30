using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace LampacTgBot;

public sealed class TelegramBot
{
    private readonly ITelegramBotClient _client;
    private readonly ProjectSession _session;
    private readonly OpenAiClient _openAiClient;

    public TelegramBot(ITelegramBotClient client, ProjectSession session, OpenAiClient openAiClient)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _openAiClient = openAiClient ?? throw new ArgumentNullException(nameof(openAiClient));
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _client.StartReceiving(HandleUpdateAsync, HandlePollingErrorAsync, receiverOptions, cancellationToken);

        var me = await _client.GetMeAsync(cancellationToken);
        Console.WriteLine($"Запущен бот @{me.Username}.");

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            // ignore
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { } message)
        {
            return;
        }

        if (!string.IsNullOrEmpty(message.Text))
        {
            await HandleTextMessageAsync(message, cancellationToken);
            return;
        }

        if (message.Voice is not null || message.Audio is not null || message.Photo?.Any() == true || message.Document is not null)
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, "Пока могу обработать только текстовые вопросы о Lampac.", cancellationToken: cancellationToken);
            return;
        }

        await botClient.SendTextMessageAsync(message.Chat.Id, "Пожалуйста, отправьте текстовый вопрос по проекту Lampac.", cancellationToken: cancellationToken);
    }

    private async Task HandleTextMessageAsync(Message message, CancellationToken cancellationToken)
    {
        var text = message.Text!.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            await _client.SendTextMessageAsync(message.Chat.Id, "Привет! Я помощник по проекту Lampac. Задавай вопросы строго по этому репозиторию.", cancellationToken: cancellationToken);
            return;
        }

        if (text.StartsWith("/help", StringComparison.OrdinalIgnoreCase))
        {
            var help = "Я отвечаю кратко и по делу, только по репозиторию Lampac. Отправляйте текстовые вопросы. Вопросы вне темы — отказ.";
            await _client.SendTextMessageAsync(message.Chat.Id, help, cancellationToken: cancellationToken);
            return;
        }

        if (text.StartsWith('/'))
        {
            await _client.SendTextMessageAsync(message.Chat.Id, "Неизвестная команда. Используйте /start или /help.", cancellationToken: cancellationToken);
            return;
        }

        if (!Utils.IsLampacRelated(text))
        {
            await _client.SendTextMessageAsync(message.Chat.Id, "Я отвечаю только по проекту Lampac. Уточните вопрос в рамках этого репозитория.", cancellationToken: cancellationToken);
            return;
        }

        var relevantChunks = _session.FindRelevantChunks(text, 5);
        if (relevantChunks.Count == 0)
        {
            await _client.SendTextMessageAsync(message.Chat.Id, "Контекст не найден в Lampac для этого запроса. Уточните вопрос в рамках проекта.", cancellationToken: cancellationToken);
            return;
        }

        var contextBuilder = new StringBuilder();
        foreach (var chunk in relevantChunks)
        {
            contextBuilder.AppendLine($"[{chunk.Path} #{chunk.Index}]");
            contextBuilder.AppendLine(chunk.Content);
            contextBuilder.AppendLine();
            if (contextBuilder.Length > 8000)
            {
                break;
            }
        }

        string context = contextBuilder.ToString().Trim();
        string reply;
        try
        {
            reply = await _openAiClient.AskAsync(context, text, message.From?.Id.ToString(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Ошибка OpenAI: {ex}");
            await _client.SendTextMessageAsync(message.Chat.Id, "Произошла ошибка при обращении к OpenAI. Попробуйте позже.", cancellationToken: cancellationToken);
            return;
        }

        var parts = Utils.SplitForTelegram(reply);
        foreach (var part in parts)
        {
            await _client.SendTextMessageAsync(message.Chat.Id, part, cancellationToken: cancellationToken);
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        string errorMessage = exception switch
        {
            ApiRequestException apiException => $"Telegram API Error: {apiException.ErrorCode} — {apiException.Message}",
            _ => $"Telegram polling error: {exception}"
        };

        Console.Error.WriteLine(errorMessage);
        return Task.CompletedTask;
    }
}
