using Lampac.Engine.CORE;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Lampac.Engine.CRON
{
    public static class LampaCron
    {
        async public static Task Run()
        {
            while (true)
            {
                try
                {
                    async ValueTask<bool> update()
                    {
                        if (!AppInit.conf.autoupdatelampahtml)
                            return false;

                        if (!File.Exists("wwwroot/lampa-main/app.min.js"))
                            return true;

                        string gitapp = await HttpClient.Get("https://raw.githubusercontent.com/yumata/lampa/main/app.min.js");
                        if (gitapp == null || !gitapp.Contains("author: 'Yumata'"))
                            return false;

                        string currentapp = await File.ReadAllTextAsync("wwwroot/lampa-main/app.min.js");

                        if (CrypTo.md5(gitapp) != CrypTo.md5(currentapp))
                            return true;

                        return false;
                    }

                    if (await update())
                    {
                        byte[] array = await HttpClient.Download("https://github.com/yumata/lampa/archive/refs/heads/main.zip", MaxResponseContentBufferSize: 20_000_000, timeoutSeconds: 40);
                        if (array != null)
                        {
                            await File.WriteAllBytesAsync("wwwroot/lampa-main.zip", array);
                            ZipFile.ExtractToDirectory("wwwroot/lampa-main.zip", "wwwroot/", overwriteFiles: true);

                            string html = await File.ReadAllTextAsync("wwwroot/lampa-main/index.html");
                            html = html.Replace("</body>", "<script src=\"/lampainit.js\"></script></body>");

                            await File.WriteAllTextAsync("wwwroot/lampa-main/index.html", html);
                        }
                    }
                }
                catch { }

                await Task.Delay(1000 * 60 * 20);
            }
        }
    }
}
