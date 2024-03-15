using Lampac.Engine.CORE;
using Newtonsoft.Json;
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
        #region static
        public static DateTime lastActve = default;

        public static int tsport;

        public static string tspass = CrypTo.md5(DateTime.Now.ToBinary().ToString());

        public static string homedir;

        public static string tspath;

        public static HashSet<string> clientIps = new HashSet<string>();

        public static System.Diagnostics.Process tsprocess;
        #endregion

        #region loaded
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
                            string sh = File.ReadAllText($"{homedir}/installTorrServerLinux.sh");
                            if (sh.Contains("dirInstall=\"\""))
                            {
                                sh = Regex.Replace(sh, "dirInstall=\"\"", $"dirInstall=\"{homedir}\"");
                                File.WriteAllText($"{homedir}/installTorrServerLinux.sh", sh);
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

                while (true)
                {
                    await Task.Delay(TimeSpan.FromMinutes(2));
                    if (lastActve == default || conf.disposeTime == -1)
                        continue;

                    try
                    {
                        if (lastActve > DateTime.Now.AddMinutes(-conf.disposeTime))
                            continue;

                        tsprocess?.Dispose();
                        tsprocess = null;

                        lastActve = default;
                        clientIps.Clear();
                    }
                    catch 
                    {
                        tsprocess = null;
                        lastActve = default;
                    }
                }
            });
        }
        #endregion


        #region conf
        public bool updatets { get; set; } = true;

        public bool rdb { get; set; } = true;

        public int disposeTime { get; set; } = -1;

        public string defaultPasswd { get; set; } = "ts";


        static (ModInit, DateTime) cacheconf = default;

        public static ModInit conf
        {
            get
            {
                if (cacheconf.Item1 == null)
                {
                    if (!File.Exists("module/TorrServer.conf"))
                        return new ModInit();
                }

                var lastWriteTime = File.GetLastWriteTime("module/TorrServer.conf");

                if (cacheconf.Item2 != lastWriteTime)
                {
                    var jss = new JsonSerializerSettings
                    {
                        Error = (se, ev) =>
                        {
                            ev.ErrorContext.Handled = true;
                            Console.WriteLine("module/TorrServer.conf - " + ev.ErrorContext.Error + "\n\n");
                        }
                    };

                    string json = File.ReadAllText("module/TorrServer.conf");
                    cacheconf.Item1 = JsonConvert.DeserializeObject<ModInit>(json, jss);

                    if (cacheconf.Item1 == null)
                        cacheconf.Item1 = new ModInit();

                    cacheconf.Item2 = lastWriteTime;
                }

                return cacheconf.Item1;
            }
        }
        #endregion
    }
}
