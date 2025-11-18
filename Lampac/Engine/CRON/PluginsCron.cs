using Shared;
using Shared.Engine;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lampac.Engine.CRON
{
    public static class PluginsCron
    {
        public static void Run()
        {
            _cronTimer = new Timer(cron, null, TimeSpan.FromMinutes(2), TimeSpan.FromHours(1));
        }

        static Timer _cronTimer;

        static bool _cronWork = false;

        async static void cron(object state)
        {
            if (_cronWork)
                return;

            _cronWork = true;

            try
            {
                if (!AppInit.conf.pirate_store)
                    return;

                async ValueTask update(string url, string checkcode = "Lampa.", string path = null)
                {
                    try
                    {
                        string js = await Http.Get(url, Encoding.UTF8, weblog: false);
                        if (js != null && js.Contains(checkcode))
                        {
                            if (path == null)
                                path = Path.GetFileName(url);

                            if (js.Contains("METRIKA"))
                                js = js.Replace("$('body').append(METRIKA);", "");

                            File.WriteAllText($"wwwroot/plugins/{path}", js, Encoding.UTF8);
                        }
                    }
                    catch { }
                }

                await update("https://immisterio.github.io/bwa/fx.js");
                await update("https://adultjs.onrender.com", path: "adult.js");
                await update("https://nb557.github.io/plugins/online_mod.js");
                await update("http://github.freebie.tom.ru/want.js");
                await update("https://nb557.github.io/plugins/reset_subs.js");
                await update("http://193.233.134.21/plugins/mult.js");
                await update("https://nemiroff.github.io/lampa/select_weapon.js");
                await update("https://nb557.github.io/plugins/not_mobile.js");
                await update("http://cub.red/plugin/etor", path: "etor.js");
                await update("http://193.233.134.21/plugins/checker.js");
                await update("https://plugin.rootu.top/ts-preload.js");
                await update("https://lampame.github.io/main/pubtorr/pubtorr.js");
                await update("https://lampame.github.io/main/nc/nc.js");
                await update("https://nb557.github.io/plugins/rating.js");
                await update("https://github.freebie.tom.ru/torrents.js");
                await update("https://nnmdd.github.io/lampa_hotkeys/hotkeys.js");
                await update("https://bazzzilius.github.io/scripts/gold_theme.js");
                await update("https://bdvburik.github.io/rezkacomment.js");
                await update("https://lampame.github.io/main/Shikimori/Shikimori.js");
            }
            catch { }
            finally
            {
                _cronWork = false;
            }
        }
    }
}
