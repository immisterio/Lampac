using LiteDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Engine;
using Shared.Models.Module;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TorrServer
{
    public class ModInit
    {
        #region static
        static bool IsShutdown;

        public static int tsport = 9080;

        public static string tspass = CrypTo.md5(DateTime.Now.ToBinary().ToString());

        public static string homedir;

        public static string tspath;

        public static Process tsprocess;
        #endregion

        #region dataDb
        static LiteDatabase dataDb;

        public static ILiteCollection<WhoseHashModel> whosehash { get; set; }
        #endregion

        #region ModInit
        public string releases { get; set; } = "MatriX.135";

        public bool rdb { get; set; }

        public string defaultPasswd { get; set; } = "ts";

        public int group { get; set; }

        /// <summary>
        /// auth
        /// full
        /// null - desable
        /// </summary>
        public string multiaccess { get; set; } = "auth";

        public bool checkfile { get; set; } = true;


        static (ModInit, DateTime) cacheconf = default;

        public static ModInit conf => cacheconf.Item1;
        #endregion

        #region cron_UpdateSettings
        static Timer _cronTimer;

        static bool _cronWork = false;

        static void cron_UpdateSettings(object state)
        {
            if (_cronWork)
                return;

            _cronWork = true;

            try
            {
                string path = "module/TorrServer.conf";

                if (!File.Exists(path))
                {
                    if (cacheconf.Item1 == null)
                        cacheconf.Item1 = new ModInit();

                    return;
                }

                var lastWriteTime = File.GetLastWriteTime(path);

                if (cacheconf.Item2 != lastWriteTime)
                {
                    cacheconf.Item1 = JsonConvert.DeserializeObject<ModInit>(File.ReadAllText(path));
                    cacheconf.Item2 = lastWriteTime;
                }
            }
            catch { }
            finally
            {
                _cronWork = false;
            }
        }
        #endregion

        #region loaded
        public static void loaded(InitspaceModel initspace)
        {
            RegisterShutdown(initspace);

            _cronTimer = new Timer(cron_UpdateSettings, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));

            dataDb = new LiteDatabase("cache/ts.db");
            whosehash = dataDb.GetCollection<WhoseHashModel>("whosehash");

            #region homedir
            homedir = Directory.GetCurrentDirectory();
            if (string.IsNullOrWhiteSpace(homedir) || homedir == "/")
                homedir = string.Empty;

            homedir = Path.Combine(homedir, "torrserver");
            Directory.CreateDirectory(homedir);
            #endregion

            #region tspath
            tspath = Path.Combine(homedir, "TorrServer-linux");

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                tspath = Path.Combine(homedir, "TorrServer-windows-amd64.exe");
            #endregion

            File.WriteAllText(Path.Combine(homedir, "accs.db"), $"{{\"ts\":\"{tspass}\"}}");

            ThreadPool.QueueUserWorkItem(async _ =>
            {
                #region downloadUrl
                string downloadUrl;
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    downloadUrl = "https://github.com/YouROK/TorrServer/releases/latest/download/TorrServer-windows-amd64.exe";
                    if (conf.releases != "latest")
                        downloadUrl = $"https://github.com/YouROK/TorrServer/releases/download/{conf.releases}/TorrServer-windows-amd64.exe";
                }
                else
                {
                    string uname = (await Bash.Run("uname -m")) ?? string.Empty;
                    string arch = uname.Contains("x86_64") ? "amd64" : (uname.Contains("i386") || uname.Contains("i686")) ? "386" : uname.Contains("aarch64") ? "arm64" : uname.Contains("armv7") ? "arm7" : uname.Contains("armv6") ? "arm5" : "amd64";

                    downloadUrl = "https://github.com/YouROK/TorrServer/releases/latest/download/TorrServer-linux-" + arch;
                    if (conf.releases != "latest")
                        downloadUrl = $"https://github.com/YouROK/TorrServer/releases/download/{conf.releases}/TorrServer-linux-" + arch;
                }
                #endregion

                #region updatet/install
                async Task install()
                {
                    try
                    {
                        if (conf.releases == "latest")
                        {
                            var root = await Http.Get<JObject>("https://api.github.com/repos/YouROK/TorrServer/releases/latest");
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
                                bool success = await Http.DownloadFile(downloadUrl, tspath, timeoutSeconds: 200);
                                if (!success)
                                    File.Delete(tspath);
                            }
                            else
                            {
                                tsprocess?.Dispose();
                                bool success = await Http.DownloadFile(downloadUrl, tspath, timeoutSeconds: 200);
                                if (success)
                                    Bash.Invoke($"chmod +x {tspath}");
                                else
                                    Bash.Invoke($"rm -f {tspath}");
                            }
                        }
                    }
                    catch { }
                }

                await install();

                if (!File.Exists(tspath))
                {
                    await Task.Delay(10_000);
                    await install();
                }

                if (!File.Exists("isdocker") && conf.checkfile)
                {
                    var response = await Http.ResponseHeaders(downloadUrl, timeoutSeconds: 10, allowAutoRedirect: true);
                    if (response != null && response.Content.Headers.ContentLength.HasValue && new FileInfo(tspath).Length != response.Content.Headers.ContentLength.Value)
                    {
                        File.Delete(tspath);
                        await Task.Delay(10_000);
                        await install();
                    }
                }
                #endregion

                while (!IsShutdown && File.Exists(tspath))
                {
                    try
                    {
                        tsprocess = new Process();
                        tsprocess.StartInfo.UseShellExecute = false;
                        tsprocess.StartInfo.RedirectStandardOutput = true;
                        tsprocess.StartInfo.RedirectStandardError = true;
                        tsprocess.StartInfo.FileName = tspath;
                        tsprocess.StartInfo.Arguments = $"--httpauth -p {tsport} -d \"{homedir}\"";

                        tsprocess.Start();

                        tsprocess.OutputDataReceived += (sender, args) => { };
                        tsprocess.ErrorDataReceived += (sender, args) => { };
                        tsprocess.BeginOutputReadLine();
                        tsprocess.BeginErrorReadLine();

                        await tsprocess.WaitForExitAsync();
                    }
                    catch { }

                    await Task.Delay(10_000);
                }
            });
        }
        #endregion


        #region Shutdown
        static void RegisterShutdown(InitspaceModel initspace)
        {
            if (initspace?.app?.ApplicationServices != null)
            {
                var lifetime = initspace.app.ApplicationServices.GetService<IHostApplicationLifetime>();
                lifetime?.ApplicationStopping.Register(StopTranscoding);
            }

            AppDomain.CurrentDomain.ProcessExit += (_, _) => StopTranscoding();
        }

        static void StopTranscoding()
        {
            try
            {
                IsShutdown = true;
                tsprocess?.Kill(true);
                _cronTimer.Dispose();
            }
            catch { }
        }
        #endregion
    }
}
