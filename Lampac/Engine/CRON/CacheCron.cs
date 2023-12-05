using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Lampac.Engine.CRON
{
    public static class CacheCron
    {
        async public static Task Run()
        {
            await Task.Delay(TimeSpan.FromMinutes(2));

            while (true)
            {
                foreach (var conf in new List<(string path, int day)> { 
                    ("html", AppInit.conf.fileCacheInactiveDay.html), 
                    ("img", AppInit.conf.fileCacheInactiveDay.img),
                    ("hls", AppInit.conf.fileCacheInactiveDay.hls),
                    ("torrent", AppInit.conf.fileCacheInactiveDay.torrent) 
                })
                {
                    try
                    {
                        if (conf.day == 0)
                            continue;

                        foreach (string infile in Directory.EnumerateFiles($"cache/{conf.path}", "*", SearchOption.AllDirectories))
                        {
                            try
                            {
                                var fileinfo = new FileInfo(infile);
                                if (DateTime.Now > fileinfo.LastWriteTime.AddDays(conf.day))
                                    fileinfo.Delete();
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                await Task.Delay(TimeSpan.FromMinutes(AppInit.conf.crontime.clearCache));
            }
        }
    }
}
