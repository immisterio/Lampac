using Lampac.Engine.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TorrServer
{
    public class ModInit
    {
        public static DateTime lastActve = default;

        public static int tsport;

        public static string tspass = CrypTo.md5(DateTime.Now.ToBinary().ToString());

        public static string homedir;

        public static string tspath;

        public static HashSet<string> clientIps = new HashSet<string>();

        public static System.Diagnostics.Process tsprocess;

        public static void loaded()
        {
            if (File.Exists(@"C:\ProgramData\lampac\disabts"))
                return;

            #region homedir
            homedir = Directory.GetCurrentDirectory();
            if (string.IsNullOrWhiteSpace(homedir) || homedir == "/")
                homedir = string.Empty;

            homedir = Regex.Replace(homedir, "/$", "") + "/torrserver";
            #endregion

            #region tspath
            tspath = $"{homedir}/TorrServer-linux";

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                tspath = $"{homedir}/TorrServer-windows-amd64.exe";
            #endregion

            tsport = new Random().Next(7000, 7400);
            File.WriteAllText($"{homedir}/accs.db", $"{{\"ts\":\"{tspass}\"}}");

            ThreadPool.QueueUserWorkItem(async _ => 
            {
                if (!File.Exists(tspath))
                {
                    try
                    {
                        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                        {
                            await HttpClient.DownloadFile("https://github.com/YouROK/TorrServer/releases/latest/download/TorrServer-windows-amd64.exe", tspath);
                        }
                        else
                        {
                            string sh = await File.ReadAllTextAsync($"{homedir}/installTorrServerLinux.sh");
                            if (sh.Contains("dirInstall=\"\""))
                            {
                                sh = Regex.Replace(sh, "dirInstall=\"\"", $"dirInstall=\"{homedir}\"");
                                await File.WriteAllTextAsync($"{homedir}/installTorrServerLinux.sh", sh);
                            }

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
                    catch { }
                }

                //while (true)
                //{
                //    await Task.Delay(TimeSpan.FromMinutes(2));
                //    if (lastActve == default)
                //        continue;

                //    try
                //    {
                //        if (lastActve > DateTime.Now.AddMinutes(-20))
                //            continue;

                //        tsprocess?.Dispose();
                //        tsprocess = null;

                //        lastActve = default;
                //        clientIps.Clear();
                //    }
                //    catch { }
                //}
            });
        }
    }
}
