using Lampac;
using Lampac.Controllers;
using Lampac.Engine.CORE;
using Shared.Engine;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DLNA
{
    public class ModInit
    {
        public static void loaded()
        {
            DLNAController.Initialization();

            var init = AppInit.conf.dlna;
            Directory.CreateDirectory($"{init.path}/temp/");

            ThreadPool.QueueUserWorkItem(async _ => 
            {
                if (await FFmpeg.InitializationAsync())
                {
                    while(true)
                    {
                        await Task.Delay(TimeSpan.FromMinutes(1));

                        if (!init.enable || !init.genCover)
                            continue;

                        try
                        {
                            #region path files
                            foreach (string file in Directory.GetFiles(init.path))
                            {
                                if (!Regex.IsMatch(Path.GetExtension(file), init.coverExtension))
                                    continue;

                                string name = Path.GetFileName(file);
                                var fileinfo = new FileInfo(file);
                                if (fileinfo.Length == 0)
                                    continue;

                                string thumb = Path.Combine(init.path, $"thumbs/{CrypTo.md5(name)}.jpg");
                                if (File.Exists(thumb))
                                    continue;

                                string lockfile = Path.Combine(init.path, $"temp/{CrypTo.md5(name)}-ffmpeg.lock");
                                if (File.Exists(lockfile))
                                    continue;

                                File.Create(lockfile);

                                await FFmpeg.RunAsync(init.coverComand.Replace("{file}", file).Replace("{thumb}", thumb));

                                if (init.genPreview)
                                {
                                    string preview = Path.Combine(init.path, $"temp/{CrypTo.md5(name)}.mp4");
                                    await FFmpeg.RunAsync(init.previewComand.Replace("{file}", file).Replace("{preview}", preview));
                                }
                            }
                            #endregion

                            #region path directories
                            foreach (string folder in Directory.GetDirectories(init.path))
                            {
                                if (folder.Contains("thumbs") || folder.Contains("tmdb") || folder.Contains("temp"))
                                    continue;

                                string folder_name = Path.GetFileName(folder);
                                string folder_thumb = Path.Combine(init.path, $"thumbs/{CrypTo.md5(folder_name)}.jpg");
                                if (File.Exists(folder_thumb))
                                    continue;

                                var files = Directory.GetFiles(folder);
                                if (files.Length == 0)
                                    continue;

                                string lockfile = Path.Combine(init.path, $"temp/{CrypTo.md5(folder_name)}-ffmpeg.lock");
                                if (File.Exists(lockfile))
                                    continue;

                                File.Create(lockfile);

                                #region превью на папку
                                {
                                    await FFmpeg.RunAsync(init.coverComand.Replace("{file}", files[0]).Replace("{thumb}", folder_thumb));

                                    if (init.genPreview)
                                    {
                                        string preview = Path.Combine(init.path, $"temp/{CrypTo.md5(folder_name)}.mp4");
                                        await FFmpeg.RunAsync(init.previewComand.Replace("{file}", files[0]).Replace("{preview}", preview));
                                    }
                                }
                                #endregion

                                foreach (string file in files)
                                {
                                    string name = $"{Path.GetFileName(folder)}/{Path.GetFileName(file)}";
                                    string thumb = Path.Combine(init.path, $"thumbs/{CrypTo.md5(name)}.jpg");
                                    await FFmpeg.RunAsync(init.coverComand.Replace("{file}", file).Replace("{thumb}", thumb));
                                }
                            }
                            #endregion
                        }
                        catch { }
                    }
                }
            });
        }
    }
}
