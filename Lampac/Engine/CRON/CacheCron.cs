using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Lampac.Engine.CRON
{
    public static class CacheCron
    {
        public static void Run()
        {
            _cronTimer = new Timer(cron, null, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(4));
        }

        static Timer _cronTimer;

        static bool _cronWork = false;

        static void cron(object state)
        {
            if (_cronWork)
                return;

            _cronWork = true;

            try
            {
                var files = new Dictionary<string, FileInfo>();
                long freeDiskSpace = getFreeDiskSpace();

                foreach (var conf in new List<(string path, int minute)> {
                    ("tmdb", AppInit.conf.tmdb.cache_img),
                    ("cub", AppInit.conf.cub.cache_img),
                    ("img", AppInit.conf.serverproxy.image.cache_time),
                    ("torrent", AppInit.conf.fileCacheInactive.torrent),
                    ("html", AppInit.conf.fileCacheInactive.html),
                    ("hls", AppInit.conf.fileCacheInactive.hls),
                    ("storage/temp", 10)
                })
                {
                    try
                    {
                        if (conf.minute == -1 || !Directory.Exists(Path.Combine("cache", conf.path)))
                            continue;

                        foreach (string infile in Directory.EnumerateFiles(Path.Combine("cache", conf.path), "*", SearchOption.AllDirectories))
                        {
                            try
                            {
                                if (conf.minute == 0)
                                    File.Delete(infile);
                                else
                                {
                                    var fileinfo = new FileInfo(infile);
                                    if (DateTime.Now > fileinfo.LastWriteTime.AddMinutes(conf.minute))
                                        fileinfo.Delete();
                                    else if (1073741824 > freeDiskSpace) // 1Gb
                                        files.TryAdd(infile, fileinfo);
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                if (files.Count > 0)
                {
                    long removeGb = 0;

                    foreach (var item in files.OrderBy(i => i.Value.LastWriteTime))
                    {
                        try
                        {
                            if (File.Exists(item.Key))
                            {
                                File.Delete(item.Key);
                                removeGb += item.Value.Length;

                                // 2Gb
                                if (removeGb > 2147483648)
                                    break;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
            finally 
            {
                _cronWork = false;
            }
        }


        static long getFreeDiskSpace()
        {
            try
            {
                var directory = new DirectoryInfo("cache");
                var drive = DriveInfo.GetDrives()
                    .FirstOrDefault(d => d.IsReady && directory.FullName.StartsWith(d.RootDirectory.FullName, StringComparison.OrdinalIgnoreCase));
                return drive?.AvailableFreeSpace ?? -1;
            }
            catch
            {
                return -1;
            }
        }
    }
}
