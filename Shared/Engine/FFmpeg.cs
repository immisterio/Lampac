using Lampac;
using Lampac.Engine.CORE;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Shared.Engine
{
    public static class FFmpeg
    {
        #region InitializationAsync
        async public static ValueTask<bool> InitializationAsync()
        {
            if (AppInit.Win32NT)
            {
                if (File.Exists("data/ffmpeg.exe"))
                    return true;

                string version = await HttpClient.Get("https://ffbinaries.com/api/v1/version/latest");
                version = Regex.Match(version ?? "", "\"version\":\"([^\"]+)\"").Groups[1].Value;

                if (!string.IsNullOrEmpty(version))
                {
                    if (await HttpClient.DownloadFile($"https://github.com/ffbinaries/ffbinaries-prebuilt/releases/download/v{version}/ffmpeg-{version}-win-64.zip", "data/ffmpeg.zip"))
                    {
                        ZipFile.ExtractToDirectory("data/ffmpeg.zip", "data/", overwriteFiles: true);
                        File.Delete("data/ffmpeg.zip");
                        return true;
                    }

                    File.Delete("data/ffmpeg.zip");
                }

                return false;
            }
            else
            {
                string version = await Bash.Run("ffmpeg -version");
                if (version == null || !version.Contains("FFmpeg developers"))
                {
                    await Bash.Run("apt update && apt install -y ffmpeg");
                    version = await Bash.Run("ffmpeg -version");
                    if (version == null || !version.Contains("FFmpeg developers"))
                        return false;
                }

                return true;
            }
        }
        #endregion

        #region RunAsync
        async public static ValueTask<(string outputData, string errorData)> RunAsync(string comand, string workingDirectory = null)
        {
            try
            {
                var process = new Process();
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.FileName = AppInit.Win32NT ? "data/ffmpeg.exe" : "ffmpeg";
                process.StartInfo.Arguments = comand;
                process.StartInfo.WorkingDirectory = workingDirectory;
                process.Start();

                string outputData = string.Empty, errorData = string.Empty;

                process.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                        outputData += args.Data;
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                        errorData += args.Data;
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                return (outputData, errorData);
            }
            catch 
            {
                return default;
            }
        }
        #endregion
    }
}
