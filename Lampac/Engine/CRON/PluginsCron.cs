using Lampac.Engine.CORE;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Lampac.Engine.CRON
{
    public static class PluginsCron
    {
        async public static Task Run()
        {
            await Task.Delay(TimeSpan.FromMinutes(2));

            while (true)
            {
                try
                {
                    async void update(string url, string checkcode = "Lampa.", string path = null)
                    {
                        try
                        {
                            string js = await HttpClient.Get(url, Encoding.UTF8);
                            if (js != null && js.Contains(checkcode))
                            {
                                if (path == null)
                                    path = Path.GetFileName(url);

                                if (js.Contains("METRIKA"))
                                    js = js.Replace("$('body').append(METRIKA);", "");

                                await File.WriteAllTextAsync($"wwwroot/plugins/{path}", js, Encoding.UTF8);
                            }
                        }
                        catch { }
                    }

                    update("https://nb557.github.io/plugins/online_mod.js");
                    update("https://bwa.to/plugins/prestige.js");
                    update("http://github.freebie.tom.ru/want.js");
                    update("https://nb557.github.io/plugins/reset_subs.js");
                    update("http://95.215.8.180/plugins/mult.js");
                    update("https://nemiroff.github.io/lampa/select_weapon.js");
                    update("https://nb557.github.io/plugins/not_mobile.js");
                    update("https://scabrum.github.io/plugins/jackett.js");
                    update("http://cub.red/plugin/etor", path: "etor.js");
                    update("http://95.215.8.180/checker.js");
                    update("https://plugin.rootu.top/ts-preload.js");
                }
                catch { }

                await Task.Delay(TimeSpan.FromMinutes(40));
            }
        }
    }
}
