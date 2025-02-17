using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Lampac.Engine.CRON
{
    public static class CacheCron
    {
        async public static Task Run()
        {
            await Task.Delay(TimeSpan.FromMinutes(2)).ConfigureAwait(false);

            while (true)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(4)).ConfigureAwait(false);

                    foreach (var conf in new List<(string path, int minute)> {
                        ("tmdb", Math.Max(AppInit.conf.tmdb.cache_img, 5)),
                        ("img", AppInit.conf.fileCacheInactive.img),
                        ("torrent", AppInit.conf.fileCacheInactive.torrent),
                        ("html", AppInit.conf.fileCacheInactive.html),
                        ("hls", AppInit.conf.fileCacheInactive.hls)
                    })
                    {
                        try
                        {
                            if (conf.minute == -1)
                                continue;

                            long folderSize = 0;
                            var files = new Dictionary<string, FileInfo>();

                            foreach (string infile in Directory.EnumerateFiles($"cache{(AppInit.Win32NT ? "\\" : "/")}{conf.path}", "*", SearchOption.AllDirectories))
                            {
                                try
                                {
                                    var fileinfo = new FileInfo(infile);
                                    if (conf.minute == 0 || DateTime.Now > fileinfo.LastWriteTime.AddMinutes(conf.minute))
                                        fileinfo.Delete();
                                    else
                                    {
                                        files.TryAdd(infile, fileinfo);
                                        folderSize += fileinfo.Length;
                                    }
                                }
                                catch { }
                            }

                            long maxcachesize = AppInit.conf.fileCacheInactive.maxcachesize * 1024 * 1024;

                            if (folderSize > maxcachesize)
                            {
                                foreach (var item in files.OrderBy(i => i.Value.LastWriteTime))
                                {
                                    try
                                    {
                                        File.Delete(item.Key);
                                        folderSize += item.Value.Length;

                                        if (maxcachesize > folderSize)
                                            break;
                                    }
                                    catch { }
                                }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
    }
}
