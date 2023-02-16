using JacRed.Engine;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;

namespace Jackett
{
    public class ModInit
    {
        #region ModInit
        static (ModInit, DateTime) cacheconf = default;

        public static ModInit conf
        {
            get
            {
                if (cacheconf.Item1 == null)
                {
                    if (!File.Exists("module/JacRed.conf"))
                        return new ModInit();
                }

                var lastWriteTime = File.GetLastWriteTime("module/JacRed.conf");

                if (cacheconf.Item2 != lastWriteTime)
                {
                    cacheconf.Item1 = JsonConvert.DeserializeObject<ModInit>(File.ReadAllText("module/JacRed.conf"));
                    cacheconf.Item2 = lastWriteTime;
                }

                return cacheconf.Item1;
            }
        }
        #endregion

        public static void loaded()
        {
            Directory.CreateDirectory("cache/jacred");
            ThreadPool.QueueUserWorkItem(async _ => await SyncCron.Run());
        }


        public string syncapi = null;

        public bool mergeduplicates = true;

        public string[] trackers = new string[] { "rutracker", "rutor", "kinozal", "nnmclub", "megapeer", "bitru", "toloka", "lostfilm", "baibako", "torrentby", "hdrezka", "selezen", "animelayer", "anilibria", "anifilm" };

        public int maxreadfile = 80;

        public bool evercache = false;
    }
}
