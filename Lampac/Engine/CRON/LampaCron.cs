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
            await Task.Delay(TimeSpan.FromMinutes(2));

            while (true)
            {
                try
                {
                    async ValueTask<bool> update()
                    {
                        if (!AppInit.conf.LampaWeb.autoupdate)
                            return false;

                        if (!File.Exists("wwwroot/lampa-main/app.min.js"))
                            return true;

                        string gitapp = await HttpClient.Get("https://raw.githubusercontent.com/yumata/lampa/main/app.min.js");
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
                        byte[] array = await HttpClient.Download("https://github.com/yumata/lampa/archive/refs/heads/main.zip", MaxResponseContentBufferSize: 20_000_000, timeoutSeconds: 40);
                        if (array != null)
                        {
                            currentapp = null;

                            await File.WriteAllBytesAsync("wwwroot/lampa-main.zip", array);
                            ZipFile.ExtractToDirectory("wwwroot/lampa-main.zip", "wwwroot/", overwriteFiles: true);

                            string html = File.ReadAllText("wwwroot/lampa-main/index.html");
                            html = html.Replace("</body>", "<script src=\"/lampainit.js\"></script></body>");

                            File.WriteAllText("wwwroot/lampa-main/index.html", html);

                            File.CreateText("wwwroot/lampa-main/personal.lampa");
                        }
                    }
                }
                catch { }

                await Task.Delay(TimeSpan.FromMinutes(Math.Max(AppInit.conf.LampaWeb.intervalupdate, 1)));
            }
        }
    }
}
