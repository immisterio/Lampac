using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Threading;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Shared.Engine
{
    /// <summary>
    /// Codex AI - Module Repository
    /// </summary>
    public static class ModuleRepository
    {
        private const string RepositoryFile = "module/repository.yaml";
        private const string StateFile = "module/.repository_state.json";

        private static readonly object SyncRoot = new object();
        private static readonly HttpClient HttpClient;

        private static ApplicationPartManager partManager;
        private static Dictionary<string, string> repositoryState;

        static ModuleRepository()
        {
            HttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };

            if (!HttpClient.DefaultRequestHeaders.UserAgent.Any())
                HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("LampacModuleRepository/1.0");

            if (!HttpClient.DefaultRequestHeaders.Accept.Any())
                HttpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        }

        public static void Configuration(IMvcBuilder mvcBuilder)
        {
            partManager = mvcBuilder?.PartManager;

            UpdateModules();
        }

        private static void UpdateModules()
        {
            if (!Monitor.TryEnter(SyncRoot))
            {
                Console.WriteLine("ModuleRepository: UpdateModules skipped because another update is running");
                return;
            }

            Console.WriteLine("ModuleRepository: UpdateModules start");

            try
            {
                var repositories = LoadConfiguration();
                if (repositories.Count == 0)
                {
                    Console.WriteLine("ModuleRepository: no repositories configured");
                    return;
                }

                Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "module"));
                Console.WriteLine("ModuleRepository: ensured module directory exists");

                var state = LoadState();
                bool stateChanged = false;
                var modulesToCompile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var repository in repositories)
                {
                    try
                    {
                        if (!repository.IsValid)
                        {
                            Console.WriteLine($"ModuleRepository: skipping invalid repository '{repository?.Url}'");
                            continue;
                        }

                        bool missingModule = repository.Folders.Any(folder => !Directory.Exists(Path.Combine(Environment.CurrentDirectory, "module", folder.ModuleName)));
                        string commitSha = GetLatestCommitSha(repository);
                        if (string.IsNullOrEmpty(commitSha))
                        {
                            Console.WriteLine($"ModuleRepository: could not determine latest commit for {repository.Url}");
                            continue;
                        }

                        string stateKey = repository.StateKey;
                        if (!missingModule && state.TryGetValue(stateKey, out string storedSha) && string.Equals(storedSha, commitSha, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"ModuleRepository: repository '{repository.Url}' is up-to-date (sha={commitSha})");
                            continue;
                        }

                        if (DownloadAndExtract(repository, modulesToCompile))
                        {
                            state[stateKey] = commitSha;
                            stateChanged = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ModuleRepository: error processing repository {repository?.Url} - {ex.Message}");
                    }
                }

                if (stateChanged)
                {
                    SaveState(state);
                    Console.WriteLine("ModuleRepository: state saved");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"module repository: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("ModuleRepository: UpdateModules finished, releasing lock");
                Monitor.Exit(SyncRoot);
            }
        }

        private static List<RepositoryEntry> LoadConfiguration()
        {
            string path = Path.Combine(Environment.CurrentDirectory, RepositoryFile.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                Console.WriteLine($"ModuleRepository: repository config file not found at {path}");
                return new List<RepositoryEntry>();
            }

            try
            {
                string yaml = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(yaml))
                {
                    Console.WriteLine("ModuleRepository: repository config file is empty");
                    return new List<RepositoryEntry>();
                }

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                var document = deserializer.Deserialize(new StringReader(yaml));
                if (document == null)
                {
                    Console.WriteLine("ModuleRepository: repository config deserialized to null");
                    return new List<RepositoryEntry>();
                }

                var repos = ParseRepositories(document);
                Console.WriteLine($"ModuleRepository: loaded {repos.Count} repository entries from config");
                return repos;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"module repository: failed to read configuration - {ex.Message}");
                return new List<RepositoryEntry>();
            }
        }

        private static List<RepositoryEntry> ParseRepositories(object document)
        {
            var list = new List<RepositoryEntry>();

            if (document is IList<object> sequence)
            {
                foreach (var item in sequence)
                {
                    var repository = CreateRepository(item);
                    if (repository != null)
                        list.Add(repository);
                    else
                        Console.WriteLine("ModuleRepository: skipped invalid repository entry in sequence");
                }
            }
            else if (document is IDictionary<object, object> map)
            {
                foreach (var entry in map)
                {
                    var repository = CreateRepository(entry.Value);
                    if (repository != null)
                        list.Add(repository);
                    else
                        Console.WriteLine("ModuleRepository: skipped invalid repository entry in map");
                }
            }

            return list;
        }

        private static RepositoryEntry CreateRepository(object node)
        {
            if (node is IDictionary<object, object> map)
            {
                string url = GetString(map, "repository", "repo", "url", "git", "remote");
                if (string.IsNullOrWhiteSpace(url))
                {
                    Console.WriteLine("ModuleRepository: repository entry missing url");
                    return null;
                }

                string branch = GetString(map, "branch", "ref");
                var folders = ParseFolders(map);

                var repository = new RepositoryEntry
                {
                    Url = url.Trim(),
                    Branch = string.IsNullOrWhiteSpace(branch) ? null : branch.Trim(),
                    Folders = folders
                };

                if (!TryParseGitHubUrl(repository.Url, out string owner, out string name))
                {
                    Console.WriteLine($"module repository: unsupported repository url '{repository.Url}'");
                    return null;
                }

                repository.Owner = owner;
                repository.Name = name;

                ApplyAuthenticationSettings(map, repository);
                Console.WriteLine($"ModuleRepository: parsed repository {repository.Owner}/{repository.Name} branch={repository.Branch ?? "(default)"}");

                // If no folders were specified in YAML, try to fetch top-level directories from GitHub repo
                if (repository.Folders == null || repository.Folders.Count == 0)
                {
                    try
                    {
                        var remoteFolders = FetchRepositoryFolders(repository);
                        if (remoteFolders.Count > 0)
                        {
                            repository.Folders = remoteFolders;
                            Console.WriteLine($"ModuleRepository: populated {remoteFolders.Count} folders from remote repository {repository.Owner}/{repository.Name}");
                        }
                        else
                        {
                            Console.WriteLine($"ModuleRepository: no folders found in remote repository {repository.Owner}/{repository.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ModuleRepository: failed to fetch folders for {repository.Owner}/{repository.Name} - {ex.Message}");
                    }
                }

                return repository;
            }

            return null;
        }

        private static void ApplyAuthenticationSettings(IDictionary<object, object> map, RepositoryEntry repository)
        {
            if (map == null || repository == null)
                return;

            string accept = GetString(map, "accept", "accept_header");
            if (!string.IsNullOrWhiteSpace(accept))
                repository.AcceptHeader = accept.Trim();

            string authHeader = GetString(map, "auth_header", "authorization", "authorization_header");
            if (!string.IsNullOrWhiteSpace(authHeader))
            {
                string resolvedHeader = ResolveSecretValue(authHeader, "auth_header", repository);
                if (!string.IsNullOrWhiteSpace(resolvedHeader))
                    repository.Token = resolvedHeader.Trim();

                return;
            }

            string tokenValue = GetString(map, "token", "pat", "personal_access_token");
            if (string.IsNullOrWhiteSpace(tokenValue))
                return;

            string resolvedToken = ResolveSecretValue(tokenValue, "token", repository);
            if (string.IsNullOrWhiteSpace(resolvedToken))
                return;

            string tokenType = GetString(map, "token_type", "auth_type", "authorization_scheme", "scheme", "token_scheme");
            string headerValue;

            if (!string.IsNullOrWhiteSpace(tokenType))
            {
                headerValue = $"{tokenType.Trim()} {resolvedToken.Trim()}".Trim();
            }
            else
            {
                string trimmed = resolvedToken.Trim();
                headerValue = trimmed.Contains(' ') ? trimmed : $"token {trimmed}";
            }

            if (string.IsNullOrWhiteSpace(headerValue))
            {
                Console.WriteLine($"ModuleRepository: resolved token for {repository.Url} is empty");
                return;
            }

            repository.Token = headerValue;
        }

        private static string ResolveSecretValue(string value, string fieldName, RepositoryEntry repository)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string trimmed = value.Trim();

            int envIndex = trimmed.IndexOf("env:", StringComparison.OrdinalIgnoreCase);
            if (envIndex < 0)
                return trimmed;

            var builder = new StringBuilder();
            int currentIndex = 0;

            while (envIndex >= 0)
            {
                builder.Append(trimmed, currentIndex, envIndex - currentIndex);

                int nameStart = envIndex + 4;
                int nameEnd = nameStart;
                while (nameEnd < trimmed.Length && (char.IsLetterOrDigit(trimmed[nameEnd]) || trimmed[nameEnd] == '_'))
                    nameEnd++;

                if (nameEnd == nameStart)
                {
                    Console.WriteLine($"ModuleRepository: {fieldName} environment variable name is missing for repository {repository?.Url}");
                    return null;
                }

                string envName = trimmed[nameStart..nameEnd];
                string envValue = Environment.GetEnvironmentVariable(envName);
                if (string.IsNullOrWhiteSpace(envValue))
                {
                    Console.WriteLine($"ModuleRepository: environment variable '{envName}' not found for repository {repository?.Url}");
                    return null;
                }

                builder.Append(envValue.Trim());
                currentIndex = nameEnd;
                envIndex = trimmed.IndexOf("env:", currentIndex, StringComparison.OrdinalIgnoreCase);
            }

            builder.Append(trimmed[currentIndex..]);
            return builder.ToString().Trim();
        }

        private static List<RepositoryFolder> ParseFolders(IDictionary<object, object> map)
        {
            foreach (string key in new[] { "modules", "folders", "directories", "paths", "include" })
            {
                if (TryGetValue(map, key, out object value))
                    return ConvertToFolders(value);
            }

            return new List<RepositoryFolder>();
        }

        private static List<RepositoryFolder> ConvertToFolders(object value)
        {
            var result = new List<RepositoryFolder>();

            if (value is IList<object> sequence)
            {
                foreach (var item in sequence)
                {
                    var folder = ConvertFolderItem(item);
                    if (folder != null)
                        result.Add(folder);
                    else
                        Console.WriteLine("ModuleRepository: skipped invalid folder item in sequence");
                }
            }
            else if (value is IDictionary<object, object> map)
            {
                foreach (var entry in map)
                {
                    var folder = ConvertFolderEntry(entry.Key, entry.Value);
                    if (folder != null)
                        result.Add(folder);
                    else
                        Console.WriteLine("ModuleRepository: skipped invalid folder entry in map");
                }
            }

            return result;
        }

        private static RepositoryFolder ConvertFolderItem(object item)
        {
            if (item is string str)
                return CreateFolder(str, null);

            if (item is IDictionary<object, object> map)
            {
                string source = GetString(map, "path", "source", "folder", "repo_path", "from");
                string target = GetString(map, "target", "name", "to", "destination");

                if (string.IsNullOrEmpty(source) && map.Count == 1)
                {
                    var single = map.First();
                    source = single.Key?.ToString();
                    target = single.Value?.ToString();
                }

                return CreateFolder(source, target);
            }

            return null;
        }

        private static RepositoryFolder ConvertFolderEntry(object key, object value)
        {
            if (value is IDictionary<object, object> map)
            {
                string source = GetString(map, "path", "source", "folder", "repo_path", "from") ?? key?.ToString();
                string target = GetString(map, "target", "name", "to", "destination") ?? value?.ToString();
                return CreateFolder(source, target);
            }

            return CreateFolder(key?.ToString(), value?.ToString());
        }

        private static RepositoryFolder CreateFolder(string source, string target)
        {
            if (string.IsNullOrWhiteSpace(source))
                return null;

            var folder = new RepositoryFolder(source, target);
            if (!folder.IsValid)
                return null;

            return folder;
        }

        private static string GetString(IDictionary<object, object> map, params string[] keys)
        {
            foreach (var key in keys)
            {
                foreach (var entry in map)
                {
                    if (string.Equals(entry.Key?.ToString(), key, StringComparison.OrdinalIgnoreCase))
                        return entry.Value?.ToString();
                }
            }

            return null;
        }

        private static bool TryGetValue(IDictionary<object, object> map, string key, out object value)
        {
            foreach (var entry in map)
            {
                if (string.Equals(entry.Key?.ToString(), key, StringComparison.OrdinalIgnoreCase))
                {
                    value = entry.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static Dictionary<string, string> LoadState()
        {
            if (repositoryState != null)
                return repositoryState;

            string path = Path.Combine(Environment.CurrentDirectory, StateFile.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (data != null)
                        repositoryState = new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"module repository: failed to load state - {ex.Message}");
                }
            }

            repositoryState ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Console.WriteLine($"ModuleRepository: loaded state entries = {repositoryState.Count}");
            return repositoryState;
        }

        private static void SaveState(Dictionary<string, string> state)
        {
            try
            {
                string path = Path.Combine(Environment.CurrentDirectory, StateFile.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonConvert.SerializeObject(state, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"module repository: failed to save state - {ex.Message}");
            }
        }

        private static string GetLatestCommitSha(RepositoryEntry repository)
        {
            if (string.IsNullOrEmpty(repository.Owner) || string.IsNullOrEmpty(repository.Name))
            {
                Console.WriteLine("ModuleRepository: GetLatestCommitSha - owner or name is empty");
                return null;
            }

            // Determine a usable branch (try configured, default, then main, then master)
            var branch = DetermineBranch(repository);
            if (string.IsNullOrEmpty(branch))
            {
                Console.WriteLine($"ModuleRepository: could not determine a valid branch for {repository.Owner}/{repository.Name}");
                return null;
            }

            var branchInfo = GetJson(repository, $"https://api.github.com/repos/{repository.Owner}/{repository.Name}/branches/{Uri.EscapeDataString(branch)}");
            var sha = branchInfo?["commit"]?["sha"]?.Value<string>();
            Console.WriteLine($"ModuleRepository: latest commit sha for {repository.Owner}/{repository.Name} ({branch}) = {sha}");
            return sha;
        }

        private static string DetermineBranch(RepositoryEntry repository)
        {
            if (string.IsNullOrEmpty(repository.Owner) || string.IsNullOrEmpty(repository.Name))
                return null;

            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(repository.Branch))
                candidates.Add(repository.Branch.Trim());

            // Try to get default branch from repo metadata
            var repoInfo = GetJson(repository, $"https://api.github.com/repos/{repository.Owner}/{repository.Name}");
            var defaultBranch = repoInfo?["default_branch"]?.Value<string>();
            if (!string.IsNullOrWhiteSpace(defaultBranch) && !candidates.Contains(defaultBranch, StringComparer.OrdinalIgnoreCase))
                candidates.Add(defaultBranch);

            // Add common fallbacks
            if (!candidates.Contains("main", StringComparer.OrdinalIgnoreCase))
                candidates.Add("main");
            if (!candidates.Contains("master", StringComparer.OrdinalIgnoreCase))
                candidates.Add("master");

            foreach (var b in candidates)
            {
                if (string.IsNullOrWhiteSpace(b))
                    continue;

                var branchInfo = GetJson(repository, $"https://api.github.com/repos/{repository.Owner}/{repository.Name}/branches/{Uri.EscapeDataString(b)}");
                if (branchInfo != null)
                {
                    repository.Branch = b;
                    Console.WriteLine($"ModuleRepository: selected branch '{b}' for {repository.Owner}/{repository.Name}");
                    return b;
                }
            }

            return null;
        }

        private static HttpResponseMessage SendGetRequest(string url, RepositoryEntry repository, string acceptOverride = null, bool includeConfiguredAccept = true)
        {
            var request = CreateRequest(HttpMethod.Get, url, repository, acceptOverride, includeConfiguredAccept);

            try
            {
                return HttpClient.SendAsync(request).GetAwaiter().GetResult();
            }
            finally
            {
                request.Dispose();
            }
        }

        private static HttpRequestMessage CreateRequest(HttpMethod method, string url, RepositoryEntry repository, string acceptOverride, bool includeConfiguredAccept)
        {
            var request = new HttpRequestMessage(method, url);

            if (!string.IsNullOrWhiteSpace(repository?.Token))
                request.Headers.TryAddWithoutValidation("Authorization", repository.Token);

            if (includeConfiguredAccept && !string.IsNullOrWhiteSpace(repository?.AcceptHeader))
                request.Headers.TryAddWithoutValidation("Accept", repository.AcceptHeader);

            if (!string.IsNullOrWhiteSpace(acceptOverride))
                request.Headers.TryAddWithoutValidation("Accept", acceptOverride);

            return request;
        }

        private static JObject GetJson(RepositoryEntry repository, string url)
        {
            try
            {
                using var response = SendGetRequest(url, repository);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"module repository: request {url} failed with {(int)response.StatusCode} {response.StatusCode}");
                    return null;
                }

                string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (string.IsNullOrEmpty(json))
                    return null;

                Console.WriteLine($"ModuleRepository: GetJson success for {url}");
                return JsonConvert.DeserializeObject<JObject>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"module repository: request {url} failed - {ex.Message}");
                return null;
            }
        }

        private static JArray GetJsonArray(RepositoryEntry repository, string url)
        {
            try
            {
                using var response = SendGetRequest(url, repository);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"module repository: request {url} failed with {(int)response.StatusCode} {response.StatusCode}");
                    return null;
                }

                string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (string.IsNullOrEmpty(json))
                    return null;

                Console.WriteLine($"ModuleRepository: GetJsonArray success for {url}");
                return JsonConvert.DeserializeObject<JArray>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"module repository: request {url} failed - {ex.Message}");
                return null;
            }
        }

        private static List<RepositoryFolder> FetchRepositoryFolders(RepositoryEntry repository)
        {
            var result = new List<RepositoryFolder>();
            if (string.IsNullOrEmpty(repository.Owner) || string.IsNullOrEmpty(repository.Name))
                return result;

            var branch = DetermineBranch(repository);
            if (string.IsNullOrEmpty(branch))
                return result;

            string url = $"https://api.github.com/repos/{repository.Owner}/{repository.Name}/contents?ref={Uri.EscapeDataString(branch)}";
            var items = GetJsonArray(repository, url);
            if (items == null)
                return result;

            foreach (var item in items)
            {
                var type = item["type"]?.Value<string>();
                if (!string.Equals(type, "dir", StringComparison.OrdinalIgnoreCase))
                    continue;

                var name = item["name"]?.Value<string>();
                if (string.IsNullOrEmpty(name))
                    continue;

                var folder = new RepositoryFolder(name, null);
                if (folder.IsValid)
                    result.Add(folder);
            }

            return result;
        }

        private static bool DownloadAndExtract(RepositoryEntry repository, HashSet<string> modulesToCompile)
        {
            string branch = string.IsNullOrWhiteSpace(repository.Branch) ? "main" : repository.Branch;
            string archiveUrl = $"https://codeload.github.com/{repository.Owner}/{repository.Name}/zip/refs/heads/{Uri.EscapeDataString(branch)}";
            string tempZip = Path.Combine(Path.GetTempPath(), $"lampac-modrepo-{Guid.NewGuid():N}.zip");
            string tempDir = Path.Combine(Path.GetTempPath(), $"lampac-modrepo-{Guid.NewGuid():N}");

            Console.WriteLine($"ModuleRepository: DownloadAndExtract start for {repository.Owner}/{repository.Name} branch={branch}");

            try
            {
                Console.WriteLine($"ModuleRepository: downloading archive {archiveUrl}");
                using (var response = SendGetRequest(archiveUrl, repository))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"module repository: failed to download {archiveUrl} - {(int)response.StatusCode}{response.StatusCode}");
                        return false;
                    }

                    using (var stream = File.Create(tempZip))
                        response.Content.CopyToAsync(stream).GetAwaiter().GetResult();
                }

                ZipFile.ExtractToDirectory(tempZip, tempDir, true);
                Console.WriteLine($"ModuleRepository: archive extracted to {tempDir}");

                string root = Directory.GetDirectories(tempDir).FirstOrDefault();
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                {
                    Console.WriteLine("module repository: archive structure not recognized");
                    return false;
                }

                Console.WriteLine($"ModuleRepository: archive root = {root}");

                foreach (var folder in repository.Folders)
                {
                    string sourcePath = folder.GetSourcePath(root);
                    if (!Directory.Exists(sourcePath))
                    {
                        Console.WriteLine($"module repository: folder '{folder.Source}' not found in {repository.Url}");
                        continue;
                    }

                    string destinationPath = Path.Combine(Environment.CurrentDirectory, "module", folder.ModuleName);

                    string existingManifestJson = null;
                    string existingManifestPath = Path.Combine(destinationPath, "manifest.json");

                    if (Directory.Exists(destinationPath))
                    {
                        try
                        {
                            // Read existing manifest if present so we can preserve/merge its values
                            if (File.Exists(existingManifestPath))
                            {
                                try { existingManifestJson = File.ReadAllText(existingManifestPath); } catch { existingManifestJson = null; }
                            }

                            Directory.Delete(destinationPath, true);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"module repository: failed to clean '{destinationPath}': {ex.Message}");
                            continue;
                        }
                    }

                    Directory.CreateDirectory(destinationPath);
                    CopyDirectory(sourcePath, destinationPath);

                    // After copying, merge manifests if we had an existing one
                    string newManifestPath = Path.Combine(destinationPath, "manifest.json");
                    if (!string.IsNullOrEmpty(existingManifestJson) && File.Exists(newManifestPath))
                    {
                        try
                        {
                            string newManifestJson = File.ReadAllText(newManifestPath);
                            var merged = MergeManifests(existingManifestJson, newManifestJson);
                            if (merged != null)
                                File.WriteAllText(newManifestPath, merged);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"module repository: failed to merge post-copy manifest.json for '{folder.ModuleName}': {ex.Message}");
                        }
                    }

                    modulesToCompile.Add(folder.ModuleName);
                    Console.WriteLine($"module repository: updated module '{folder.ModuleName}' from {repository.Url}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"module repository: {ex.Message}");
                return false;
            }
            finally
            {
                try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
                Console.WriteLine($"ModuleRepository: DownloadAndExtract finished for {repository.Owner}/{repository.Name}");
            }
        }

        private static string MergeManifests(string existingJson, string newJson)
        {
            try
            {
                var existingToken = JsonConvert.DeserializeObject<JToken>(existingJson);
                var newToken = JsonConvert.DeserializeObject<JToken>(newJson);

                if (existingToken == null)
                    return newJson;

                if (newToken == null)
                    return existingJson;

                // If both are arrays: merge by 'dll' key; start from existing to preserve custom fields
                if (existingToken is JArray existingArr && newToken is JArray newArr)
                {
                    // Build index for existing by dll (case-insensitive)
                    var existingIndex = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
                    foreach (var e in existingArr.OfType<JObject>())
                    {
                        var dll = e["dll"]?.Value<string>();
                        if (!string.IsNullOrEmpty(dll))
                            existingIndex[dll.ToLowerInvariant()] = (JObject)e.DeepClone();
                    }

                    // Apply updates from newArr: only properties present in source overwrite existing
                    foreach (var n in newArr.OfType<JObject>())
                    {
                        var ndll = n["dll"]?.Value<string>();
                        if (!string.IsNullOrEmpty(ndll) && existingIndex.TryGetValue(ndll.ToLowerInvariant(), out JObject existObj))
                        {
                            foreach (var prop in n.Properties())
                            {
                                var name = prop.Name;
                                if (string.Equals(name, "enable", StringComparison.OrdinalIgnoreCase))
                                {
                                    // preserve existing enable if present
                                    if (existObj.Property(name, StringComparison.OrdinalIgnoreCase) == null)
                                        existObj[name] = prop.Value.DeepClone();

                                    continue;
                                }

                                // Only update properties that exist in new manifest (we are iterating them)
                                existObj[name] = prop.Value.DeepClone();
                            }

                            existingIndex[ndll.ToLowerInvariant()] = existObj;
                        }
                        else
                        {
                            // New entry: add to existingIndex
                            var clone = (JObject)n.DeepClone();
                            existingIndex[ndll?.ToLowerInvariant() ?? Guid.NewGuid().ToString()] = clone;
                        }
                    }

                    // Preserve original order where possible: start with original existingArr order, then append any new ones not present
                    var resultArr = new JArray();

                    var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var e in existingArr.OfType<JObject>())
                    {
                        var dll = e["dll"]?.Value<string>() ?? string.Empty;
                        if (existingIndex.TryGetValue(dll.ToLowerInvariant(), out JObject val))
                        {
                            resultArr.Add(val);
                            added.Add(dll.ToLowerInvariant());
                        }
                        else
                        {
                            resultArr.Add(e);
                            added.Add(dll.ToLowerInvariant());
                        }
                    }

                    // Append remaining
                    foreach (var kv in existingIndex)
                    {
                        if (!added.Contains(kv.Key))
                            resultArr.Add(kv.Value);
                    }

                    return JsonConvert.SerializeObject(resultArr, Formatting.Indented);
                }

                // If both are objects: merge into existing, updating only fields present in new, but preserve existing enable
                if (existingToken is JObject existingObjRoot && newToken is JObject newObjRoot)
                {
                    foreach (var prop in newObjRoot.Properties())
                    {
                        var name = prop.Name;
                        if (string.Equals(name, "enable", StringComparison.OrdinalIgnoreCase) && existingObjRoot.Property(name, StringComparison.OrdinalIgnoreCase) != null)
                            continue; // preserve

                        existingObjRoot[name] = prop.Value.DeepClone();
                    }

                    return JsonConvert.SerializeObject(existingObjRoot, Formatting.Indented);
                }

                // Fallback: return newJson
                return newJson;
            }
            catch
            {
                return newJson;
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, directory);
                if (ShouldSkip(relative))
                    continue;

                Directory.CreateDirectory(Path.Combine(destination, relative));
            }

            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, file);
                if (ShouldSkip(relative))
                    continue;

                string target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));

                try
                {
                    File.Copy(file, target, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"module repository: failed to copy file '{file}' to '{target}': {ex.Message}");
                }
            }
        }

        private static bool ShouldSkip(string relative)
        {
            if (string.IsNullOrEmpty(relative))
                return false;

            string normalized = relative.Replace('\\', '/');
            if (normalized.StartsWith(".git", StringComparison.OrdinalIgnoreCase) || normalized.StartsWith(".github", StringComparison.OrdinalIgnoreCase))
                return true;

            string fileName = Path.GetFileName(normalized);
            if (string.Equals(fileName, ".gitignore", StringComparison.OrdinalIgnoreCase) || string.Equals(fileName, ".gitattributes", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static bool TryParseGitHubUrl(string url, out string owner, out string name)
        {
            owner = null;
            name = null;

            if (string.IsNullOrWhiteSpace(url))
                return false;

            string working = url.Trim();

            if (working.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
            {
                int index = working.IndexOf(':');
                if (index != -1 && working.Length > index + 1)
                    working = working[(index + 1)..];
            }

            if (!working.StartsWith("http", StringComparison.OrdinalIgnoreCase) && working.Contains("github.com"))
                working = "https://" + working.TrimStart('/');

            if (Uri.TryCreate(working, UriKind.Absolute, out var uri) && uri.Host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase))
            {
                var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 2)
                {
                    owner = segments[0];
                    name = segments[1];
                }
            }
            else
            {
                var parts = working.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    owner = parts[^2];
                    name = parts[^1];
                }
            }

            if (!string.IsNullOrEmpty(name) && name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                name = name[..^4];

            return !string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(name);
        }

        private sealed class RepositoryEntry
        {
            public string Url { get; set; }
            public string Branch { get; set; }
            public string Owner { get; set; }
            public string Name { get; set; }
            public string Token { get; set; }
            public string AcceptHeader { get; set; }
            public List<RepositoryFolder> Folders { get; set; } = new List<RepositoryFolder>();

            public bool IsValid => !string.IsNullOrEmpty(Url) && !string.IsNullOrEmpty(Owner) && !string.IsNullOrEmpty(Name) && Folders.Count > 0;

            public string StateKey => $"repo:{Url}|{Branch}";
        }

        private sealed class RepositoryFolder
        {
            public RepositoryFolder(string source, string target)
            {
                Source = Normalize(source);
                ModuleName = NormalizeTarget(target, Source);
            }

            public string Source { get; }

            public string ModuleName { get; }

            public bool IsValid => !string.IsNullOrEmpty(Source) && !string.IsNullOrEmpty(ModuleName);

            private static string Normalize(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return null;

                string trimmed = value.Trim().Replace('\\', '/').Trim('/');
                if (trimmed.Contains(".."))
                    return null;

                return trimmed;
            }

            private static string NormalizeTarget(string target, string source)
            {
                string normalized = Normalize(target);
                if (string.IsNullOrEmpty(normalized))
                    normalized = Normalize(source);

                if (string.IsNullOrEmpty(normalized))
                    return null;

                var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length == 0)
                    return null;

                return segments[^1].Replace('/', Path.DirectorySeparatorChar);
            }

            public string GetSourcePath(string root)
            {
                string path = root;
                foreach (string part in Source.Split('/', StringSplitOptions.RemoveEmptyEntries))
                    path = Path.Combine(path, part);

                return path;
            }
        }
    }
}