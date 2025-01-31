using Lampac.Engine.CORE;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Lampac.Engine.CRON
{
    public static class LampaCron
    {
        static string currentapp;

        async public static Task Run()
        {
            await Task.Delay(TimeSpan.FromMinutes(2)).ConfigureAwait(false);

            while (true)
            {
                try
                {
                    var init = AppInit.conf.LampaWeb;

                    async ValueTask<bool> update()
                    {
                        if (!init.autoupdate)
                            return false;

                        if (!File.Exists("wwwroot/lampa-main/app.min.js"))
                            return true;

                        string gitapp = await HttpClient.Get($"https://raw.githubusercontent.com/yumata/lampa/{(string.IsNullOrEmpty(init.tree) ? "main" : init.tree)}/app.min.js", weblog: false);
                        if (gitapp == null || !gitapp.Contains("author: 'Yumata'"))
                            return false;

                        if (currentapp == null)
                        {
                            currentapp = File.ReadAllText("wwwroot/lampa-main/app.min.js");
                            currentapp = CrypTo.md5(currentapp);
                        }

                        if (CrypTo.md5(gitapp) != currentapp)
                            return true;

                        return false;
                    }

                    if (await update())
                    {
                        string uri = string.IsNullOrEmpty(init.tree) ? 
                                     "https://github.com/yumata/lampa/archive/refs/heads/main.zip" :
                                     $"https://github.com/yumata/lampa/archive/{init.tree}.zip";

                        byte[] array = await HttpClient.Download(uri, MaxResponseContentBufferSize: 20_000_000, timeoutSeconds: 40);
                        if (array != null)
                        {
                            currentapp = null;

                            await File.WriteAllBytesAsync("wwwroot/lampa.zip", array);
                            ZipFile.ExtractToDirectory("wwwroot/lampa.zip", "wwwroot/", overwriteFiles: true);

                            if (!string.IsNullOrEmpty(init.tree))
                            {
                                string plugins_black_list = File.Exists("wwwroot/lampa-main/plugins_black_list.json") ? File.ReadAllText("wwwroot/lampa-main/plugins_black_list.json") : "[]";
                                string modification = File.Exists("wwwroot/lampa-main/plugins/modification.js") ? File.ReadAllText("wwwroot/lampa-main/plugins/modification.js") : string.Empty;

                                Directory.Move("wwwroot/lampa-main", $"wwwroot/lampa-old-{DateTime.Now.ToFileTime()}");
                                Directory.Move($"wwwroot/lampa-{init.tree}", "wwwroot/lampa-main");

                                Directory.CreateDirectory("wwwroot/lampa-main/plugins");
                                File.WriteAllText("wwwroot/lampa-main/plugins_black_list.json", plugins_black_list);
                                File.WriteAllText("wwwroot/lampa-main/plugins/modification.js", modification);
                            }
                            else
                            {
                                if (!File.Exists("wwwroot/lampa-main/plugins_black_list.json"))
                                    File.WriteAllText("wwwroot/lampa-main/plugins_black_list.json", "[]");

                                if (!File.Exists("wwwroot/lampa-main/plugins/modification.js"))
                                {
                                    Directory.CreateDirectory("wwwroot/lampa-main/plugins");
                                    File.WriteAllText("wwwroot/lampa-main/plugins/modification.js", string.Empty);
                                }
                            }

                            string html = File.ReadAllText("wwwroot/lampa-main/index.html");
                            html = html.Replace("</body>", "<script src=\"/lampainit.js\"></script></body>");

                            File.WriteAllText("wwwroot/lampa-main/index.html", html);
                            File.CreateText("wwwroot/lampa-main/personal.lampa");
                            File.Delete("wwwroot/lampa.zip");
                        }
                    }
                }
                catch { }

                await Task.Delay(TimeSpan.FromMinutes(Math.Max(AppInit.conf.LampaWeb.intervalupdate, 1))).ConfigureAwait(false);
            }
        }
    }
}
