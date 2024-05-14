using Lampac.Engine.CORE;
using System;
using System.IO;
using System.IO.Compression;
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
                if (!AppInit.conf.pirate_store)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1));
                    continue;
                }

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

                                File.WriteAllText($"wwwroot/plugins/{path}", js, Encoding.UTF8);
                            }
                        }
                        catch { }
                    }

                    update("https://nb557.github.io/plugins/online_mod.js");
                    update("https://bwa.to/plugins/prestige.js");
                    update("http://github.freebie.tom.ru/want.js");
                    update("https://nb557.github.io/plugins/reset_subs.js");
                    update("http://193.233.134.21/plugins/mult.js");
                    update("https://nemiroff.github.io/lampa/select_weapon.js");
                    update("https://nb557.github.io/plugins/not_mobile.js");
                    update("http://cub.red/plugin/etor", path: "etor.js");
                    update("http://193.233.134.21/plugins/checker.js");
                    update("https://plugin.rootu.top/ts-preload.js");
                    update("https://lampame.github.io/main/pubtorr/pubtorr.js");
                    update("https://lampame.github.io/main/nc/nc.js");
                    update("https://nb557.github.io/plugins/rating.js");
                    update("https://github.freebie.tom.ru/torrents.js");
                    update("https://nnmdd.github.io/lampa_hotkeys/hotkeys.js");
                    update("https://bazzzilius.github.io/scripts/gold_theme.js");
                    update("https://bdvburik.github.io/rezkacomment.js");
                }
                catch { }

                try
                {
                    if (File.Exists("wwwroot/bwa/_framework/blazor.boot.json"))
                    {
                        string bootapp = await HttpClient.Get("https://bwa.pages.dev/blazor.boot.json");
                        if (bootapp != null && bootapp.Contains("JinEnergy.wasm"))
                        {
                            string currentapp = File.ReadAllText("wwwroot/bwa/_framework/blazor.boot.json");
                            currentapp = CrypTo.md5(currentapp);

                            if (CrypTo.md5(bootapp) != currentapp)
                            {
                                byte[] array = await HttpClient.Download("https://bwa.pages.dev/latest.zip", MaxResponseContentBufferSize: 20_000_000, timeoutSeconds: 40);
                                if (array != null)
                                {
                                    await File.WriteAllBytesAsync("wwwroot/bwa/latest.zip", array);
                                    ZipFile.ExtractToDirectory("wwwroot/bwa/latest.zip", "wwwroot/bwa/_framework/", overwriteFiles: true);
                                    File.Delete("wwwroot/bwa/latest.zip");
                                }
                            }
                        }
                    }
                }
                catch { }

                await Task.Delay(TimeSpan.FromMinutes(40));
            }
        }
    }
}
