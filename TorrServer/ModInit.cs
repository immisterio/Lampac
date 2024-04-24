using Lampac.Controllers;
using Lampac.Engine.CORE;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Engine;
using System;
using System.Collections.Generic;
using System.IO;
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

        public static TaskCompletionSource<bool> taskCompletionSource { get; set; }
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
            Directory.CreateDirectory(homedir);
            #endregion

            #region tspath
            tspath = $"{homedir}/TorrServer-linux";

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                tspath = $"{homedir}/TorrServer-windows-amd64.exe";
            #endregion

            tsport = new Random().Next(7000, 7400);
            File.WriteAllText($"{homedir}/accs.db", $"{{\"ts\":\"{tspass}\"}}");

            #region update/install TorrServer
            ThreadPool.QueueUserWorkItem(async _ =>
            {
                while (true)
                {
                    try
                    {
                        if (conf.updatets)
                        {
                            var root = await HttpClient.Get<JObject>("https://api.github.com/repos/YouROK/TorrServer/releases/latest");
                            if (root != null && root.ContainsKey("tag_name"))
                            {
                                string tagname = root.Value<string>("tag_name");
                                if (!string.IsNullOrEmpty(tagname))
                                {
                                    if (!File.Exists($"{homedir}/tagname") || tagname != File.ReadAllText($"{homedir}/tagname"))
                                    {
                                        if (File.Exists(tspath))
                                            File.Delete(tspath);

                                        File.WriteAllText($"{homedir}/tagname", tagname);
                                    }
                                }
                            }
                        }

                        if (!File.Exists(tspath))
                        {
                            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                            {
                                tsprocess?.Dispose();
                                await HttpClient.DownloadFile("https://github.com/YouROK/TorrServer/releases/latest/download/TorrServer-windows-amd64.exe", tspath);
                            }
                            else
                            {
                                string uname = await Bash.Run("uname -m");
                                string arch = uname.Contains("i386") || uname.Contains("i686") ? "386" : uname.Contains("x86_64") ? "amd64" : uname.Contains("aarch64") ? "arm64" : uname.Contains("armv7") ? "arm7" : uname.Contains("armv6") ? "arm5" : null;

                                tsprocess?.Dispose();
                                await HttpClient.DownloadFile("https://github.com/YouROK/TorrServer/releases/latest/download/TorrServer-linux-" + arch, tspath);

                                await Bash.Run($"chmod +x {tspath}");
                            }
                        }

                        if (conf.disposeTime == -1 && tsprocess == null)
                            await TorrServerController.Start(null);
                    }
                    catch { }

                    await Task.Delay(TimeSpan.FromHours(1));
                }
            });
            #endregion

            #region disposeTime
            ThreadPool.QueueUserWorkItem(async _ => 
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1));
                    if (lastActve == default || tsprocess == null || conf.disposeTime == -1)
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
            #endregion
        }
        #endregion


        #region conf
        public bool updatets { get; set; }

        public bool rdb { get; set; }

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
