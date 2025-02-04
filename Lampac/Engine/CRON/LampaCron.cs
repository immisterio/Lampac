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
            await Task.Delay(TimeSpan.FromMinutes(1)).ConfigureAwait(false);

            while (true)
            {
                try
                {
                    var init = AppInit.conf.LampaWeb;
                    bool istree = !string.IsNullOrEmpty(init.tree);

                    async ValueTask<bool> update()
                    {
                        if (!init.autoupdate)
                            return false;

                        if (!File.Exists("wwwroot/lampa-main/app.min.js"))
                            return true;

                        if (istree && File.Exists($"wwwroot/lampa-main/{init.tree}"))
                            return false;

                        string gitapp = await HttpClient.Get($"https://raw.githubusercontent.com/yumata/lampa/{(istree ? init.tree : "main")}/app.min.js", weblog: false);
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
                        string uri = istree ? $"https://github.com/yumata/lampa/archive/{init.tree}.zip" :
                                              "https://github.com/yumata/lampa/archive/refs/heads/main.zip";

                        byte[] array = await HttpClient.Download(uri, MaxResponseContentBufferSize: 20_000_000, timeoutSeconds: 40);
                        if (array != null)
                        {
                            currentapp = null;

                            await File.WriteAllBytesAsync("wwwroot/lampa.zip", array);
                            ZipFile.ExtractToDirectory("wwwroot/lampa.zip", "wwwroot/", overwriteFiles: true);

                            if (istree)
                            {
                                string plugins_black_list = File.Exists("wwwroot/lampa-main/plugins_black_list.json") ? File.ReadAllText("wwwroot/lampa-main/plugins_black_list.json") : "[]";
                                string modification = File.Exists("wwwroot/lampa-main/plugins/modification.js") ? File.ReadAllText("wwwroot/lampa-main/plugins/modification.js") : string.Empty;

                                if (Directory.Exists("wwwroot/lampa-main"))
                                    Directory.Move("wwwroot/lampa-main", $"wwwroot/lampa-old-{DateTime.Now.ToFileTime()}");

                                Directory.Move($"wwwroot/lampa-{init.tree}", "wwwroot/lampa-main");
                                File.WriteAllText($"wwwroot/lampa-main/{init.tree}", string.Empty);

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
