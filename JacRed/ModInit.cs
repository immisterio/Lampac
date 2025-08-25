using JacRed.Models.AppConf;
using Newtonsoft.Json;
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
                    var jss = new JsonSerializerSettings { Error = (se, ev) => 
                    { 
                        ev.ErrorContext.Handled = true; 
                        Console.WriteLine("module/JacRed.conf - " + ev.ErrorContext.Error + "\n\n"); 
                    }};

                    string json = File.ReadAllText("module/JacRed.conf");
                    if (!json.Trim().StartsWith("{"))
                        json = "{"+json+"}";

                    cacheconf.Item1 = JsonConvert.DeserializeObject<ModInit>(json, jss);
                    cacheconf.Item2 = lastWriteTime;
                }

                return cacheconf.Item1;
            }
        }
        #endregion

        public static void loaded()
        {
            Directory.CreateDirectory("cache/jacred");
            File.WriteAllText("module/JacRed.current.conf", JsonConvert.SerializeObject(conf, Formatting.Indented));

            ThreadPool.QueueUserWorkItem(async _ => await SyncCron.Run().ConfigureAwait(false));
            ThreadPool.QueueUserWorkItem(async _ => await FileDB.Cron().ConfigureAwait(false));
            ThreadPool.QueueUserWorkItem(async _ => await FileDB.CronFast().ConfigureAwait(false));


            ThreadPool.QueueUserWorkItem(async _ =>
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromMinutes(5)).ConfigureAwait(false);

                    try
                    {
                        if (conf.typesearch == "jackett" || conf.merge == "jackett")
                        {
                            async ValueTask<bool> showdown(string name, TrackerSettings settings)
                            {
                                if (!settings.monitor_showdown)
                                    return false;

                                var proxyManager = new ProxyManager(name, settings);
                                string html = await Http.Get($"{settings.host}", timeoutSeconds: conf.Jackett.timeoutSeconds, proxy: proxyManager.Get(), weblog: false).ConfigureAwait(false);
                                return html == null;
                            }

                            conf.Jackett.Rutor.showdown = await showdown("rutor", conf.Jackett.Rutor).ConfigureAwait(false);
                            conf.Jackett.Megapeer.showdown = await showdown("megapeer", conf.Jackett.Megapeer).ConfigureAwait(false);
                            conf.Jackett.TorrentBy.showdown = await showdown("torrentby", conf.Jackett.TorrentBy).ConfigureAwait(false);
                            conf.Jackett.Kinozal.showdown = await showdown("kinozal", conf.Jackett.Kinozal).ConfigureAwait(false);
                            conf.Jackett.NNMClub.showdown = await showdown("nnmclub", conf.Jackett.NNMClub).ConfigureAwait(false);
                            conf.Jackett.Bitru.showdown = await showdown("bitru", conf.Jackett.Bitru).ConfigureAwait(false);
                            conf.Jackett.Toloka.showdown = await showdown("toloka", conf.Jackett.Toloka).ConfigureAwait(false);
                            conf.Jackett.Rutracker.showdown = await showdown("rutracker", conf.Jackett.Rutracker).ConfigureAwait(false);
                            conf.Jackett.BigFanGroup.showdown = await showdown("bigfangroup", conf.Jackett.BigFanGroup).ConfigureAwait(false);
                            conf.Jackett.Selezen.showdown = await showdown("selezen", conf.Jackett.Selezen).ConfigureAwait(false);
                            conf.Jackett.Lostfilm.showdown = await showdown("lostfilm", conf.Jackett.Lostfilm).ConfigureAwait(false);
                            conf.Jackett.Anilibria.showdown = await showdown("anilibria", conf.Jackett.Anilibria).ConfigureAwait(false);
                            conf.Jackett.Animelayer.showdown = await showdown("animelayer", conf.Jackett.Animelayer).ConfigureAwait(false);
                            conf.Jackett.Anifilm.showdown = await showdown("anifilm", conf.Jackett.Anifilm).ConfigureAwait(false);
                        }
                    }
                    catch { }
                }
            });
        }


        /// <summary>
        /// red
        /// jackett
        /// webapi
        /// </summary>
        public string typesearch = "webapi";

        public string merge = "jackett";

        public string webApiHost = "http://redapi.cfhttp.top";

        public string filter { get; set; }

        public string filter_ignore { get; set; }


        public RedConf Red = new RedConf();

        public JacConf Jackett = new JacConf();
    }
}
