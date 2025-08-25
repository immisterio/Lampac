using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Shared;
using Shared.Engine;

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
                var init = AppInit.conf.LampaWeb;
                bool istree = !string.IsNullOrEmpty(init.tree);

                try
                {
                    async ValueTask<bool> update()
                    {
                        if (!init.autoupdate)
                            return false;

                        if (!File.Exists("wwwroot/lampa-main/app.min.js"))
                            return true;

                        if (istree && File.Exists("wwwroot/lampa-main/tree") && init.tree == File.ReadAllText("wwwroot/lampa-main/tree"))
                            return false;

                        string gitapp = await Http.Get($"https://raw.githubusercontent.com/yumata/lampa/{(istree ? init.tree : "main")}/app.min.js", weblog: false).ConfigureAwait(false);
                        if (gitapp == null || !gitapp.Contains("author: 'Yumata'"))
                            return false;

                        if (currentapp == null)
                        {
                            currentapp = File.ReadAllText("wwwroot/lampa-main/app.min.js");
                            currentapp = CrypTo.md5(currentapp);
                        }

                        if (CrypTo.md5(gitapp) != currentapp)
                            return true;

                        if (istree)
                            File.WriteAllText("wwwroot/lampa-main/tree", init.tree);

                        return false;
                    }

                    if (await update().ConfigureAwait(false))
                    {
                        string uri = istree ? $"https://github.com/yumata/lampa/archive/{init.tree}.zip" :
                                              "https://github.com/yumata/lampa/archive/refs/heads/main.zip";

                        byte[] array = await Http.Download(uri, MaxResponseContentBufferSize: 20_000_000, timeoutSeconds: 40).ConfigureAwait(false);
                        if (array != null)
                        {
                            currentapp = null;

                            await File.WriteAllBytesAsync("wwwroot/lampa.zip", array).ConfigureAwait(false);
                            ZipFile.ExtractToDirectory("wwwroot/lampa.zip", "wwwroot/", overwriteFiles: true);

                            if (istree)
                            {
                                foreach (string infilePath in Directory.GetFiles($"wwwroot/lampa-{init.tree}", "*", SearchOption.AllDirectories))
                                {
                                    string outfile = infilePath.Replace($"lampa-{init.tree}", "lampa-main");
                                    Directory.CreateDirectory(Path.GetDirectoryName(outfile));
                                    File.Copy(infilePath, outfile, true);
                                }

                                File.WriteAllText("wwwroot/lampa-main/tree", init.tree);
                            }

                            string html = File.ReadAllText("wwwroot/lampa-main/index.html");
                            html = html.Replace("</body>", "<script src=\"/lampainit.js\"></script></body>");

                            File.WriteAllText("wwwroot/lampa-main/index.html", html);
                            File.CreateText("wwwroot/lampa-main/personal.lampa");

                            if (!File.Exists("wwwroot/lampa-main/plugins_black_list.json"))
                                File.WriteAllText("wwwroot/lampa-main/plugins_black_list.json", "[]");

                            if (!File.Exists("wwwroot/lampa-main/plugins/modification.js"))
                            {
                                Directory.CreateDirectory("wwwroot/lampa-main/plugins");
                                File.WriteAllText("wwwroot/lampa-main/plugins/modification.js", string.Empty);
                            }

                            File.Delete("wwwroot/lampa.zip");

                            if (istree) 
                                Directory.Delete($"wwwroot/lampa-{init.tree}", true);
                        }
                    }
                }
                catch { }

                if (!istree)
                    await Task.Delay(TimeSpan.FromMinutes(Math.Max(init.intervalupdate, 1))).ConfigureAwait(false);

                await Task.Delay(TimeSpan.FromSeconds(20)).ConfigureAwait(false);
            }
        }
    }
}
