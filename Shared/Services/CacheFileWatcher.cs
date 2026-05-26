using Shared.Services.Buckets;
using System.Collections.Concurrent;
using System.Threading;

namespace Shared.Services;

public class CacheFileWatcher
{
    #region CacheFileModel
    sealed class CacheFileModel
    {
        public int Length { get; set; }

        public long Ticks { get; set; }
    }
    #endregion

    #region static
    static readonly Serilog.ILogger Log = Serilog.Log.ForContext("SourceContext", nameof(CacheFileWatcher));

    /// <summary>
    /// <path, <md5key, CacheFileModel>
    /// </summary>
    static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, CacheFileModel>> cacheFiles = new();
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

        BucketFolders.Create(folder);

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
                        Length = (int)file.Length,
                        Ticks = file.LastWriteTimeUtc.Ticks,
                    });
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "CatchId={CatchId}", "id_uv8f7e4q");
                }
            }
        });

        if (extend == -1)
            return;

        ThreadPool.QueueUserWorkItem(async _ =>
        {
            while (!Startup.IsShutdown)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1)).ConfigureAwait(false);

                    long cutoff = DateTime.UtcNow.AddMinutes(-extend).Ticks;

                    foreach (var item in cache)
                    {
                        try
                        {
                            if (extend == 0 || cutoff > item.Value.Ticks)
                            {
                                if (cache.TryRemove(item.Key, out var _))
                                    File.Delete(OutFile(path, item.Key));
                            }
                        }
                        catch (Exception ex)
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
            msm.Position = 0;

            await using (var streamFile = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 0, options: FileOptions.Asynchronous))
                await msm.CopyToAsync(streamFile).ConfigureAwait(false);

            var md = new CacheFileModel()
            {
                Length = (int)msm.Length,
                Ticks = File.GetLastWriteTimeUtc(outFile).Ticks
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
                Length = length,
                Ticks = File.GetLastWriteTimeUtc(outFile).Ticks
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
    public void Remove(string md5key)
    {
        try
        {
            if (cacheFiles[keyPath].TryRemove(md5key, out var _f))
                File.Delete(OutFile(md5key));
        }
        catch { }
    }
    #endregion

    #region TryGetValue
    public bool TryGetValue(string key, out int length)
    {
        if (cacheFiles[keyPath].TryGetValue(key, out var cache))
        {
            length = cache.Length;
            return true;
        }

        length = 0;
        return false;
    }
    #endregion

    public string OutFile(string md5key)
        => OutFile(keyPath, md5key);

    public static string OutFile(string keyPath, string md5key)
        => Path.Combine("cache", keyPath, BucketFolders.Name(md5key[0]), md5key);

    public int FilesCount
        => cacheFiles[keyPath].Count;
}
