using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenAI.Responses;

#pragma warning disable OPENAI001

namespace LampacTgBot;

public sealed class OpenAiClient
{
    private readonly OpenAIResponseClient _client;
    private readonly string _systemInstruction;

    public OpenAiClient(string apiKey, string model, string systemInstruction)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key is required", nameof(apiKey));
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("Model name is required", nameof(model));
        }

        _client = new OpenAIResponseClient(model, apiKey);
        _systemInstruction = systemInstruction;
    }

    public async Task<string> AskAsync(string context, string question, string? userId, CancellationToken cancellationToken)
    {
        var promptBuilder = new System.Text.StringBuilder();
        promptBuilder.AppendLine("[CONTEXT]");
        promptBuilder.AppendLine(context);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("[QUESTION]");
        promptBuilder.AppendLine(question);

        var inputItems = new List<ResponseItem>
        {
            ResponseItem.CreateSystemMessageItem(_systemInstruction),
            ResponseItem.CreateUserMessageItem(promptBuilder.ToString())
        };

        // Пример стриминга (для включения раскомментируйте и адаптируйте код):
        // await foreach (var update in _client.CreateResponseStreamingAsync(inputItems, new ResponseCreationOptions(), cancellationToken))
        // {
        //     if (update is StreamingResponseOutputTextDeltaUpdate delta)
        //     {
        //         // Обрабатывайте фрагменты текста и обновляйте сообщение в Telegram.
        //     }
        // }

        var result = await _client.CreateResponseAsync(inputItems, new ResponseCreationOptions(), cancellationToken).ConfigureAwait(false);
        var response = result.Value;
        var answer = response?.GetOutputText();
        return string.IsNullOrWhiteSpace(answer) ? "(пустой ответ)" : answer.Trim();
    }
}
#pragma warning restore OPENAI001
