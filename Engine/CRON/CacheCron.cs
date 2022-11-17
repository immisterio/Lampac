using System;
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
                foreach (string path in new string[] { "html", "img", "torrent" })
                {
                    try
                    {
                        foreach (string infile in Directory.GetFiles($"cache/{path}", "*", SearchOption.AllDirectories))
                        {
                            var fileinfo = new FileInfo(infile);
                            if (DateTime.Now > fileinfo.LastWriteTime.AddDays(AppInit.conf.fileCacheInactiveDay))
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
