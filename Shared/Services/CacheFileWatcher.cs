using System.Collections.Concurrent;
using System.Threading;

namespace Shared.Services;

public class CacheFileWatcher
{
    #region static
    static readonly Serilog.ILogger Log = Serilog.Log.ForContext("SourceContext", nameof(CacheFileWatcher));

    /// <summary>
    /// <path, <md5key, CacheFileModel>
    /// </summary>
    static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, CacheFileModel>> cacheFiles = new();

    static readonly ConcurrentDictionary<string, byte> cacheDirectories = new();
    #endregion

    #region Configure
    /// <param name="path">img, tmdb, cub</param>
    /// <param name="extend">minute</param>
    public static void Configure(string path, int extend)
    {
        if (cacheFiles.ContainsKey(path))
            return;

        cacheFiles.TryAdd(path, new ConcurrentDictionary<string, CacheFileModel>());

        var cache = cacheFiles[path];
        string folder = Path.Combine("cache", path);

        if (Directory.Exists(folder))
        {
            Parallel.ForEach(Directory.GetDirectories(folder), new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            dir =>
            {
                foreach (var file in new DirectoryInfo(dir).EnumerateFiles("*", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint
                }))
                {
                    try
                    {
                        cache.TryAdd(file.Name, new CacheFileModel()
                        {
                            FullPath = file.FullName,
                            Length = (int)file.Length,
                            LastWriteTimeUtc = file.LastWriteTimeUtc,
                        });
                    }
                    catch (System.Exception ex)
                    {
                        Log.Error(ex, "CatchId={CatchId}", "id_uv8f7e4q");
                    }
                }
            });
        }
        else
        {
            Directory.CreateDirectory(folder);
        }

        if (extend == -1)
            return;

        ThreadPool.QueueUserWorkItem(async _ =>
        {
            while (!Startup.IsShutdown)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1)).ConfigureAwait(false);

                    var cutoff = DateTime.UtcNow.AddMinutes(-extend);

                    foreach (var item in cache)
                    {
                        try
                        {
                            if (extend == 0 || cutoff > item.Value.LastWriteTimeUtc)
                            {
                                if (cache.TryRemove(item.Key, out var _))
                                    File.Delete(item.Value.FullPath);
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Log.Error(ex, "CatchId={CatchId}", "id_1ijbvnkf");
                        }
                    }
                }
                catch { }
            }
        });
    }
    #endregion

    string keyPath;

    public CacheFileWatcher(string path)
    {
        keyPath = path;
    }

    #region TrySave
    /// <param name="path">img, tmdb, cub</param>
    async public Task<bool> TrySave(string md5key, Stream msm)
    {
        if (string.IsNullOrEmpty(md5key) || 2 > md5key.Length)
            return false;

        var cache = cacheFiles[keyPath];
        string outFile = OutFile(md5key);

        try
        {
            EnsureDirectory(md5key);

            msm.Position = 0;

            await using (var streamFile = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 0, options: FileOptions.Asynchronous))
                await msm.CopyToAsync(streamFile).ConfigureAwait(false);

            var md = new CacheFileModel()
            {
                FullPath = outFile,
                Length = (int)msm.Length,
                LastWriteTimeUtc = File.GetLastWriteTimeUtc(outFile)
            };

            cache.AddOrUpdate(md5key, md, (k, v) => md);

            return true;
        }
        catch
        {
            cache.TryRemove(md5key, out _);

            try
            {
                File.Delete(outFile);
            }
            catch { }

            return false;
        }
    }
    #endregion

    #region Add
    /// <param name="path">img, tmdb, cub</param>
    public bool Add(string md5key, int length)
    {
        if (string.IsNullOrEmpty(md5key) || 2 > md5key.Length)
            return false;

        var cache = cacheFiles[keyPath];
        string outFile = OutFile(md5key);

        try
        {
            var md = new CacheFileModel()
            {
                FullPath = outFile,
                Length = length,
                LastWriteTimeUtc = File.GetLastWriteTimeUtc(outFile)
            };

            cache.AddOrUpdate(md5key, md, (k, v) => md);

            return true;
        }
        catch
        {
            cache.TryRemove(md5key, out _);

            try
            {
                File.Delete(outFile);
            }
            catch { }

            return false;
        }
    }
    #endregion

    #region Remove
    /// <param name="path">img, tmdb, cub</param>
    public void Remove(string md5key)
    {
        try
        {
            if (cacheFiles[keyPath].TryRemove(md5key, out var _f))
                File.Delete(_f.FullPath);
        }
        catch { }
    }
    #endregion

    #region EnsureDirectory
    public void EnsureDirectory(string md5key)
    {
        string folder = Path.Combine("cache", keyPath, md5key.Substring(0, 2));
        cacheDirectories.GetOrAdd(folder, folder =>
        {
            Directory.CreateDirectory(folder);
            return 0;
        });
    }
    #endregion

    public bool TryGetValue(string md5key, out CacheFileModel value)
        => cacheFiles[keyPath].TryGetValue(md5key, out value);

    public string OutFile(string md5key)
        => Path.Combine("cache", keyPath, md5key.Substring(0, 2), md5key);

    public int FilesCount
        => cacheFiles[keyPath].Count;
}


public class CacheFileModel
{
    public string FullPath { get; set; }

    public int Length { get; set; }

    public DateTime LastWriteTimeUtc { get; set; }
}
