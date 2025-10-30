using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LampacTgBot;

public static class Utils
{
    private const int TelegramMessageLimit = 3800;
    private static readonly string[] LampacKeywords =
    {
        "lampac", "лампак", "immisterio", "sisi", "контроллер", "controller", "jacred", "tmdb", "trakt", "torrent", "торрент", "nginx", "addon", "плагин", "кинопоиск"
    };

    public static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LampacTgBot/1.0 (+https://github.com/immisterio/Lampac)");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/plain");
        return client;
    }

    public static async Task<string?> DownloadStringWithRetryAsync(HttpClient client, string url, CancellationToken cancellationToken, int attempts = 2)
    {
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(15));

                using var response = await client.GetAsync(url, cts.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Не удалось загрузить {url}: {response.StatusCode}");
                    continue;
                }

                if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > 200_000)
                {
                    Console.WriteLine($"Пропуск крупного файла {url} ({response.Content.Headers.ContentLength.Value} байт).");
                    return null;
                }

                return await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Console.Error.WriteLine($"Таймаут загрузки {url} (попытка {attempt}).");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Ошибка загрузки {url}: {ex.Message}");
            }
        }

        return null;
    }

    public static IReadOnlyList<string> SplitIntoChunks(string text, int maxChunkSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<string>();
        }

        var lines = text.Replace("\r\n", "\n").Split('\n');
        var chunks = new List<string>();
        var builder = new StringBuilder();

        foreach (var line in lines)
        {
            var lineToProcess = line;
            if (lineToProcess.Length == 0)
            {
                AppendLine(builder, string.Empty, chunks, maxChunkSize);
                continue;
            }

            while (lineToProcess.Length > maxChunkSize)
            {
                var slice = lineToProcess[..maxChunkSize];
                AppendLine(builder, slice, chunks, maxChunkSize);
                lineToProcess = lineToProcess[maxChunkSize..];
            }

            AppendLine(builder, lineToProcess, chunks, maxChunkSize);
        }

        if (builder.Length > 0)
        {
            var chunk = builder.ToString().Trim();
            if (!string.IsNullOrEmpty(chunk))
            {
                chunks.Add(chunk);
            }
        }

        return chunks;
    }

    private static void AppendLine(StringBuilder builder, string line, List<string> chunks, int maxChunkSize)
    {
        if (builder.Length + line.Length + Environment.NewLine.Length > maxChunkSize && builder.Length > 0)
        {
            var chunk = builder.ToString().Trim();
            if (!string.IsNullOrEmpty(chunk))
            {
                chunks.Add(chunk);
            }

            builder.Clear();
        }

        builder.AppendLine(line);
    }

    public static List<string> ExtractKeywords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        var matches = Regex.Matches(text.ToLowerInvariant(), "[\\p{L}0-9]{2,}");
        return matches.Select(m => m.Value).Distinct().ToList();
    }

    public static int CountKeywordMatches(string text, IReadOnlyList<string> keywords)
    {
        int total = 0;
        foreach (var keyword in keywords)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                continue;
            }

            int index = 0;
            while ((index = text.IndexOf(keyword, index, StringComparison.Ordinal)) >= 0)
            {
                total++;
                index += keyword.Length;
            }
        }

        return total;
    }

    public static IReadOnlyList<string> SplitForTelegram(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return new[] { "(пустой ответ)" };
        }

        if (message.Length <= TelegramMessageLimit)
        {
            return new[] { message };
        }

        var parts = new List<string>();
        int index = 0;
        while (index < message.Length)
        {
            int length = Math.Min(TelegramMessageLimit, message.Length - index);
            int lastBreak = message.LastIndexOf('\n', index + length - 1, length);
            if (lastBreak >= index && lastBreak > index)
            {
                length = lastBreak - index + 1;
            }

            var part = message.Substring(index, length).Trim();
            if (!string.IsNullOrEmpty(part))
            {
                parts.Add(part);
            }

            index += Math.Max(length, 1);
        }

        return parts;
    }

    public static bool IsLampacRelated(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var lower = text.ToLowerInvariant();
        return LampacKeywords.Any(lower.Contains);
    }
}
