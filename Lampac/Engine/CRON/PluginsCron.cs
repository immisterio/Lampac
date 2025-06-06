using Lampac.Engine.CORE;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lampac.Engine.CRON
{
    public static class PluginsCron
    {
        async public static Task Run()
        {
            await Task.Delay(TimeSpan.FromMinutes(2)).ConfigureAwait(false);

            while (true)
            {
                if (!AppInit.conf.pirate_store)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    async ValueTask update(string url, string checkcode = "Lampa.", string path = null)
                    {
                        try
                        {
                            string js = await HttpClient.Get(url, Encoding.UTF8, weblog: false).ConfigureAwait(false);
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

                    await update("https://immisterio.github.io/bwa/fx.js").ConfigureAwait(false);
                    await update("https://nb557.github.io/plugins/online_mod.js").ConfigureAwait(false);
                    await update("http://github.freebie.tom.ru/want.js").ConfigureAwait(false);
                    await update("https://nb557.github.io/plugins/reset_subs.js").ConfigureAwait(false);
                    await update("http://193.233.134.21/plugins/mult.js").ConfigureAwait(false);
                    await update("https://nemiroff.github.io/lampa/select_weapon.js").ConfigureAwait(false);
                    await update("https://nb557.github.io/plugins/not_mobile.js").ConfigureAwait(false);
                    await update("http://cub.red/plugin/etor", path: "etor.js").ConfigureAwait(false);
                    await update("http://193.233.134.21/plugins/checker.js").ConfigureAwait(false);
                    await update("https://plugin.rootu.top/ts-preload.js").ConfigureAwait(false);
                    await update("https://lampame.github.io/main/pubtorr/pubtorr.js").ConfigureAwait(false);
                    await update("https://lampame.github.io/main/nc/nc.js").ConfigureAwait(false);
                    await update("https://nb557.github.io/plugins/rating.js").ConfigureAwait(false);
                    await update("https://github.freebie.tom.ru/torrents.js").ConfigureAwait(false);
                    await update("https://nnmdd.github.io/lampa_hotkeys/hotkeys.js").ConfigureAwait(false);
                    await update("https://bazzzilius.github.io/scripts/gold_theme.js").ConfigureAwait(false);
                    await update("https://bdvburik.github.io/rezkacomment.js").ConfigureAwait(false);
                    await update("https://lampame.github.io/main/Shikimori/Shikimori.js").ConfigureAwait(false);
                }
                catch { }

                try
                {
                    if (File.Exists("wwwroot/bwa/_framework/blazor.boot.json"))
                    {
                        string bwajs = await HttpClient.Get("https://bwa.to/f", weblog: false).ConfigureAwait(false);
                        string framework = Regex.Match(bwajs, "framework = '([^']+)'").Groups[1].Value;

                        string bootapp = await HttpClient.Get($"{framework}/blazor.boot.json", weblog: false).ConfigureAwait(false);
                        if (bootapp != null && bootapp.Contains("JinEnergy.wasm"))
                        {
                            string currentapp = File.ReadAllText("wwwroot/bwa/_framework/blazor.boot.json");

                            if (CrypTo.md5(bootapp) != CrypTo.md5(currentapp))
                            {
                                byte[] array = await HttpClient.Download($"{framework}/latest.zip", MaxResponseContentBufferSize: 20_000_000, timeoutSeconds: 40).ConfigureAwait(false);
                                if (array != null)
                                {
                                    await File.WriteAllBytesAsync("wwwroot/bwa/latest.zip", array).ConfigureAwait(false);
                                    ZipFile.ExtractToDirectory("wwwroot/bwa/latest.zip", "wwwroot/bwa/_framework/", overwriteFiles: true);
                                    File.Delete("wwwroot/bwa/latest.zip");
                                }
                            }
                        }
                    }
                }
                catch { }

                await Task.Delay(TimeSpan.FromMinutes(40)).ConfigureAwait(false);
            }
        }
    }
}
