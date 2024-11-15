using Lampac;
using Lampac.Engine.CORE;
using Shared.Engine;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Tracks
{
    public class ModInit
    {
        public static async void loaded()
        {
            if (AppInit.Win32NT)
            {
                if (File.Exists(@"C:\ProgramData\lampac\disablesync") || File.Exists("cache/ffprobe.exe"))
                    return;

                string version = await HttpClient.Get("https://ffbinaries.com/api/v1/version/latest");
                version = Regex.Match(version ?? "", "\"version\":\"([^\"]+)\"").Groups[1].Value;

                if (!string.IsNullOrEmpty(version))
                {
                    if (await HttpClient.DownloadFile($"https://github.com/ffbinaries/ffbinaries-prebuilt/releases/download/v{version}/ffprobe-{version}-win-64.zip", "cache/ffprobe.zip"))
                    {
                        ZipFile.ExtractToDirectory("cache/ffprobe.zip", "cache/", overwriteFiles: true);
                        File.Delete("cache/ffprobe.zip");
                    }
                }
            }
            else
            {
                string version = await Bash.Run("ffprobe -version");
                if (version == null || !version.Contains("FFmpeg developers"))
                    await Bash.Run("apt update && apt install -y ffmpeg");
            }
        }
    }
}
