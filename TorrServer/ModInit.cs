using Lampac.Engine.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TorrServer
{
    public class ModInit
    {
        public static DateTime lastActve = default;

        public static int tsport;

        public static string tspass = CrypTo.md5(DateTime.Now.ToBinary().ToString());

        public static string homedir = "/home/lampac/torrserver";

        public static string tspath = $"{homedir}/TorrServer-linux";

        public static HashSet<string> clientIps = new HashSet<string>();

        public static System.Diagnostics.Process tsprocess;

        public static void loaded()
        {
            if (File.Exists(@"C:\ProgramData\lampac\disabts"))
                return;

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                homedir = $"{Directory.GetCurrentDirectory()}/torrserver";
                tspath = $"{homedir}/TorrServer-windows-amd64.exe";
            }

            tsport = new Random().Next(7000, 7400);
            File.WriteAllText($"{homedir}/accs.db", $"{{\"ts\":\"{tspass}\"}}");

            ThreadPool.QueueUserWorkItem(async _ => 
            {
                if (!File.Exists(tspath))
                {
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    {
                        await HttpClient.DownloadFile("https://github.com/YouROK/TorrServer/releases/latest/download/TorrServer-windows-amd64.exe", tspath);
                    }
                    else
                    {
                        var process = new System.Diagnostics.Process();
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                        process.StartInfo.FileName = "bash";
                        process.StartInfo.Arguments = $"{homedir}/installTorrServerLinux.sh";
                        process.Start();
                        await process.WaitForExitAsync();
                    }
                }

                while (true)
                {
                    await Task.Delay(TimeSpan.FromMinutes(2));
                    if (lastActve == default)
                        continue;

                    try
                    {
                        if (lastActve > DateTime.Now.AddMinutes(-20))
                            continue;

                        tsprocess?.Dispose();
                        tsprocess = null;

                        lastActve = default;
                        clientIps.Clear();
                    }
                    catch { }
                }
            });
        }
    }
}
