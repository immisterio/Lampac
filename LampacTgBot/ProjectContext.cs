using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LampacTgBot;

public sealed class ProjectContext
{
    private const string Owner = "immisterio";
    private const string Repository = "Lampac";
    private const string Branch = "HEAD";
    private static readonly string[] AdditionalCsPrefixes =
    {
        "sisi/controllers/",
        "sisi/handlers/",
        "sisi/models/",
        "shared/",
        "lampac/",
        "online/",
        "catalog/",
        "tracks/"
    };

    private readonly HttpClient _httpClient;

    public ProjectContext(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<ProjectSession> LoadAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        IReadOnlyList<GitTreeItem> tree = await FetchRepositoryTreeAsync(cancellationToken).ConfigureAwait(false);
        var candidateFiles = FilterCandidateFiles(tree);

        var documents = new List<ProjectDoc>();
        foreach (var item in candidateFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var url = BuildRawUrl(item.Path);
            string? content = await Utils.DownloadStringWithRetryAsync(_httpClient, url, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            documents.Add(new ProjectDoc(item.Path, content));
        }

        var chunks = BuildChunks(documents);

        stopwatch.Stop();
        Console.WriteLine($"Lampac контекст загружен: файлов {documents.Count}, чанков {chunks.Count}, время {stopwatch.Elapsed.TotalSeconds:F1} c.");

        return new ProjectSession(documents, chunks);
    }

    private static string BuildRawUrl(string path)
        => $"https://raw.githubusercontent.com/{Owner}/{Repository}/{Branch}/{path}";

    private async Task<IReadOnlyList<GitTreeItem>> FetchRepositoryTreeAsync(CancellationToken cancellationToken)
    {
        var requestUri = $"https://api.github.com/repos/{Owner}/{Repository}/git/trees/{Branch}?recursive=1";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<GitTreeResponse>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return payload?.Tree ?? new List<GitTreeItem>();
    }

    private static IReadOnlyList<GitTreeItem> FilterCandidateFiles(IEnumerable<GitTreeItem> tree)
    {
        var results = new List<GitTreeItem>();
        foreach (var item in tree)
        {
            if (!string.Equals(item.Type, "blob", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (item.Size.HasValue && item.Size.Value > 200_000)
            {
                continue;
            }

            var path = item.Path;
            var lower = path.ToLowerInvariant();

            if (lower is "readme" or "readme.md" or "readme.rst")
            {
                results.Add(item);
                continue;
            }

            if (!path.Contains('/'))
            {
                if (lower.EndsWith(".md") || lower.EndsWith(".rst"))
                {
                    results.Add(item);
                }

                continue;
            }

            if (lower.StartsWith("docs/") && (lower.EndsWith(".md") || lower.EndsWith(".rst") || lower.EndsWith(".txt")))
            {
                results.Add(item);
                continue;
            }

            if (lower.StartsWith("sisi/controllers/") && lower.EndsWith(".cs"))
            {
                results.Add(item);
                continue;
            }

            if (lower.EndsWith(".cs") && AdditionalCsPrefixes.Any(lower.StartsWith))
            {
                results.Add(item);
            }
        }

        return results;
    }

    private static List<ProjectChunk> BuildChunks(IEnumerable<ProjectDoc> documents)
    {
        var chunks = new List<ProjectChunk>();
        foreach (var doc in documents)
        {
            var parts = Utils.SplitIntoChunks(doc.Content, 1600);
            int index = 0;
            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                chunks.Add(new ProjectChunk(doc.Path, index++, part));
            }
        }

        return chunks;
    }

    private sealed record GitTreeResponse
    {
        [JsonPropertyName("tree")]
        public List<GitTreeItem> Tree { get; init; } = new();
    }

    private sealed record GitTreeItem
    {
        [JsonPropertyName("path")]
        public string Path { get; init; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("size")]
        public int? Size { get; init; }
    }
}

public sealed class ProjectDoc
{
    public ProjectDoc(string path, string content)
    {
        Path = path;
        Content = content;
    }

    public string Path { get; }
    public string Content { get; }
}

public sealed class ProjectChunk
{
    public ProjectChunk(string path, int index, string content)
    {
        Path = path;
        Index = index;
        Content = content;
        LowerContent = content.ToLowerInvariant();
    }

    public string Path { get; }
    public int Index { get; }
    public string Content { get; }
    public string LowerContent { get; }
}

public sealed class ProjectSession
{
    private readonly IReadOnlyList<ProjectDoc> _documents;
    private readonly IReadOnlyList<ProjectChunk> _chunks;

    public ProjectSession(IReadOnlyList<ProjectDoc> documents, IReadOnlyList<ProjectChunk> chunks)
    {
        _documents = documents;
        _chunks = chunks;
    }

    public IReadOnlyList<ProjectDoc> Documents => _documents;
    public IReadOnlyList<ProjectChunk> Chunks => _chunks;

    public IReadOnlyList<ProjectChunk> FindRelevantChunks(string query, int limit)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<ProjectChunk>();
        }

        var keywords = Utils.ExtractKeywords(query);
        if (keywords.Count == 0)
        {
            return Array.Empty<ProjectChunk>();
        }

        var scored = new List<(ProjectChunk chunk, int score)>();
        foreach (var chunk in _chunks)
        {
            int score = Utils.CountKeywordMatches(chunk.LowerContent, keywords);
            if (score > 0)
            {
                scored.Add((chunk, score));
            }
        }

        return scored
            .OrderByDescending(item => item.score)
            .ThenBy(item => item.chunk.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.chunk.Index)
            .Take(limit)
            .Select(item => item.chunk)
            .ToArray();
    }
}
