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
            var cover = init.cover;
            Directory.CreateDirectory($"{init.path}/temp/");

            ThreadPool.QueueUserWorkItem(async _ => 
            {
                bool? ffmpegInit = null;

                while (true)
                {
                    if (cover.timeout == -666)
                        await Task.Delay(TimeSpan.FromSeconds(5));
                    else
                        await Task.Delay(TimeSpan.FromMinutes(cover.timeout > 0 ? cover.timeout : 1));

                    if (!init.enable || !cover.enable)
                        continue;

                    if (ffmpegInit == null)
                    {
                        ffmpegInit = await FFmpeg.InitializationAsync();
                        if (ffmpegInit == false)
                            break;
                    }

                    try
                    {
                        #region path files
                        foreach (string file in Directory.GetFiles(init.path))
                        {
                            if (!Regex.IsMatch(Path.GetExtension(file), cover.extension))
                                continue;

                            string name = Path.GetFileName(file);
                            var fileinfo = new FileInfo(file);
                            if (fileinfo.Length == 0)
                                continue;

                            var time = fileinfo.CreationTime > fileinfo.LastWriteTime ? fileinfo.CreationTime : fileinfo.LastWriteTime;
                            if (time.AddMinutes(cover.skipModificationTime) > DateTime.Now)
                                continue;

                            string thumb = Path.Combine(init.path, "thumbs", $"{CrypTo.md5(name)}.jpg");
                            if (File.Exists(thumb))
                                continue;

                            string lockfile = Path.Combine(init.path, "temp", $"{CrypTo.md5(name)}-ffmpeg.lock");
                            if (File.Exists(lockfile))
                                continue;

                            File.Create(lockfile);

                            await FFmpeg.RunAsync(cover.coverComand.Replace("{file}", file).Replace("{thumb}", thumb), priorityClass: cover.priorityClass);

                            if (cover.preview)
                            {
                                string preview = Path.Combine(init.path, "temp", $"{CrypTo.md5(name)}.mp4");
                                await FFmpeg.RunAsync(cover.previewComand.Replace("{file}", file).Replace("{preview}", preview), priorityClass: cover.priorityClass);
                            }
                        }
                        #endregion

                        #region path directories
                        foreach (string folder in Directory.GetDirectories(init.path))
                        {
                            if (folder.Contains("thumbs") || folder.Contains("tmdb") || folder.Contains("temp"))
                                continue;

                            string folder_name = Path.GetFileName(folder);
                            string folder_thumb = Path.Combine(init.path, "thumbs", $"{CrypTo.md5(folder_name)}.jpg");
                            if (File.Exists(folder_thumb))
                                continue;

                            var files = Directory.GetFiles(folder);
                            if (files.Length == 0)
                                continue;

                            var folderinfo = new DirectoryInfo(folder);
                            var time = folderinfo.CreationTime > folderinfo.LastWriteTime ? folderinfo.CreationTime : folderinfo.LastWriteTime;
                            if (time.AddMinutes(cover.skipModificationTime) > DateTime.Now)
                                continue;

                            string lockfile = Path.Combine(init.path, "temp", $"{CrypTo.md5(folder_name)}-ffmpeg.lock");
                            if (File.Exists(lockfile))
                                continue;

                            File.Create(lockfile);

                            #region постер с превью на папку
                            {
                                await FFmpeg.RunAsync(cover.coverComand.Replace("{file}", files[0]).Replace("{thumb}", folder_thumb), priorityClass: cover.priorityClass);

                                if (cover.preview)
                                {
                                    string preview = Path.Combine(init.path, "temp", $"{CrypTo.md5(folder_name)}.mp4");
                                    await FFmpeg.RunAsync(cover.previewComand.Replace("{file}", files[0]).Replace("{preview}", preview), priorityClass: cover.priorityClass);
                                }
                            }
                            #endregion

                            #region постеры на файлы внутри папки
                            foreach (string file in files)
                            {
                                string name = $"{Path.GetFileName(folder)}/{Path.GetFileName(file)}";
                                string thumb = Path.Combine(init.path, "thumbs", $"{CrypTo.md5(name)}.jpg");
                                await FFmpeg.RunAsync(cover.coverComand.Replace("{file}", file).Replace("{thumb}", thumb), priorityClass: cover.priorityClass);
                            }
                            #endregion
                        }
                        #endregion
                    }
                    catch { }
                }

            });
        }
    }
}
