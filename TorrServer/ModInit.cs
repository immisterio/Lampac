using Lampac.Engine.CORE;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Engine;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TorrServer
{
    public class ModInit
    {
        #region static
        public static int tsport = 9080;

        public static string tspass = CrypTo.md5(DateTime.Now.ToBinary().ToString());

        public static string homedir;

        public static string tspath;

        public static Process tsprocess;
        #endregion

        #region ModInit
        public bool updatets { get; set; }

        public bool rdb { get; set; }

        public int group { get; set; }

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

        #region loaded
        public static void loaded()
        {
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

            #region update accs.db
            File.WriteAllText($"{homedir}/accs.db", $"{{\"ts\":\"{tspass}\"}}");

            //ThreadPool.QueueUserWorkItem(async _ =>
            //{
            //    while (true)
            //    {
            //        await Task.Delay(TimeSpan.FromMinutes(1)).ConfigureAwait(false);

            //        try
            //        {
            //            if (AppInit.conf.accsdb.enable)
            //            {
            //                Dictionary<string, string> accs = new Dictionary<string, string>()
            //                {
            //                    ["ts"] = tspass
            //                };

            //                foreach (var user in AppInit.conf.accsdb.accounts)
            //                    accs.TryAdd(user.Key, conf.defaultPasswd);

            //                File.WriteAllText($"{homedir}/accs.db", JsonConvert.SerializeObject(accs));
            //            }
            //        }
            //        catch { }
            //    }
            //});
            #endregion

            ThreadPool.QueueUserWorkItem(async _ =>
            {
                #region downloadUrl
                string downloadUrl;
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    downloadUrl = "https://github.com/YouROK/TorrServer/releases/latest/download/TorrServer-windows-amd64.exe";
                }
                else
                {
                    string uname = (await Bash.Run("uname -m")) ?? string.Empty;
                    string arch = uname.Contains("x86_64") ? "amd64" : (uname.Contains("i386") || uname.Contains("i686")) ? "386" : uname.Contains("aarch64") ? "arm64" : uname.Contains("armv7") ? "arm7" : uname.Contains("armv6") ? "arm5" : "amd64";

                    downloadUrl = "https://github.com/YouROK/TorrServer/releases/latest/download/TorrServer-linux-" + arch;
                }
                #endregion

                #region updatet/install
                reinstall: try
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
                        redownload: try
                        {
                            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                            {
                                tsprocess?.Dispose();
                                bool success = await HttpClient.DownloadFile(downloadUrl, tspath, timeoutSeconds: 200);
                                if (!success)
                                    File.Delete(tspath);
                            }
                            else
                            {
                                tsprocess?.Dispose();
                                bool success = await HttpClient.DownloadFile(downloadUrl, tspath, timeoutSeconds: 200);
                                if (success)
                                    await Bash.Run($"chmod +x {tspath}");
                                else
                                    await Bash.Run($"rm -f {tspath}");
                            }
                        }
                        catch
                        {
                            File.Delete(tspath);
                            await Task.Delay(10_000);
                            goto redownload;
                        }
                    }
                }
                catch { }

                if (!File.Exists(tspath))
                {
                    await Task.Delay(10_000);
                    goto reinstall;
                }

                if (!File.Exists("isdocker"))
                {
                    var response = await HttpClient.ResponseHeaders(downloadUrl, timeoutSeconds: 20, allowAutoRedirect: true);
                    if (response != null && response.Content.Headers.ContentLength.HasValue && new FileInfo(tspath).Length != response.Content.Headers.ContentLength.Value)
                    {
                        File.Delete(tspath);
                        await Task.Delay(10_000);
                        goto reinstall;
                    }
                }
                #endregion

                reset: try
                {
                    tsprocess = new Process();
                    tsprocess.StartInfo.UseShellExecute = false;
                    tsprocess.StartInfo.RedirectStandardOutput = true;
                    tsprocess.StartInfo.RedirectStandardError = true;
                    tsprocess.StartInfo.FileName = tspath;
                    tsprocess.StartInfo.Arguments = $"--httpauth -p {tsport} -d {homedir}";

                    tsprocess.Start();

                    tsprocess.OutputDataReceived += (sender, args) => { };
                    tsprocess.ErrorDataReceived += (sender, args) => { };
                    tsprocess.BeginOutputReadLine();
                    tsprocess.BeginErrorReadLine();

                    await tsprocess.WaitForExitAsync().ConfigureAwait(false);
                }
                catch { }

                await Task.Delay(10_000).ConfigureAwait(false);
                goto reset;
            });
        }
        #endregion
    }
}
