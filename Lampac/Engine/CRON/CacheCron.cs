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
                foreach (var conf in new List<(string path, int hour)> { 
                    ("html", AppInit.conf.fileCacheInactiveHour.html), 
                    ("img", AppInit.conf.fileCacheInactiveHour.img),
                    ("hls", AppInit.conf.fileCacheInactiveHour.hls),
                    ("torrent", AppInit.conf.fileCacheInactiveHour.torrent) 
                })
                {
                    try
                    {
                        if (conf.hour == -1)
                            continue;

                        foreach (string infile in Directory.EnumerateFiles($"cache/{conf.path}", "*", SearchOption.AllDirectories))
                        {
                            try
                            {
                                var fileinfo = new FileInfo(infile);
                                if (conf.hour == 0 || DateTime.Now > fileinfo.LastWriteTime.AddHours(conf.hour))
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
