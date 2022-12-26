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
            while (true)
            {
                foreach (var conf in new List<(string path, int day)> { 
                    ("html", AppInit.conf.fileCacheInactiveDay.html), 
                    ("img", AppInit.conf.fileCacheInactiveDay.img), 
                    ("torrent", AppInit.conf.fileCacheInactiveDay.torrent) 
                })
                {
                    try
                    {
                        if (conf.day == 0)
                            continue;

                        foreach (string infile in Directory.EnumerateFiles($"cache/{conf.path}", "*", SearchOption.AllDirectories))
                        {
                            var fileinfo = new FileInfo(infile);
                            if (DateTime.Now > fileinfo.LastWriteTime.AddDays(conf.day))
                                fileinfo.Delete();
                        }
                    }
                    catch { }
                }

                await Task.Delay(TimeSpan.FromMinutes(60));
            }
        }
    }
}
