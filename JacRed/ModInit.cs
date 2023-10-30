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
                    string json = File.ReadAllText("module/JacRed.conf");

                    if (json.Contains("abu.land"))
                    {
                        json = json.Replace("abu.land", "85.17.54.98");
                        File.WriteAllText("module/JacRed.conf", json);
                    }

                    cacheconf.Item1 = JsonConvert.DeserializeObject<ModInit>(json);
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

        public bool mergenumduplicates = true;

        public string[] trackers = new string[] { "rutracker", "rutor", "kinozal", "nnmclub", "megapeer", "bitru", "toloka", "lostfilm", "baibako", "torrentby", "hdrezka", "selezen", "animelayer", "anilibria", "anifilm" };

        public int maxreadfile = 200;

        public bool evercache = false;

        public int syntime = 40;
    }
}
