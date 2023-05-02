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

        public static bool enable;

        public static int tsport;

        public static string tspass = CrypTo.md5(DateTime.Now.ToBinary().ToString());

        public static string homedir = "/home/lampac/torrserver";

        public static string tspath = $"{homedir}/TorrServer-linux";

        public static HashSet<string> clientIps = new HashSet<string>();

        public static System.Diagnostics.Process tsprocess;

        public static void loaded()
        {
            enable = Environment.OSVersion.Platform != PlatformID.Win32NT;
            if (!enable)
                return;

            tsport = new Random().Next(7000, 7400);
            File.WriteAllText($"{homedir}/accs.db", $"{{\"ts\":\"{tspass}\"}}");

            ThreadPool.QueueUserWorkItem(async _ => 
            {
                if (!File.Exists(tspath))
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
