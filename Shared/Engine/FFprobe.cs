using System.Diagnostics;

namespace Shared.Engine
{
    public static class FFprobe
    {
        #region InitializationAsync
        public static Task<bool> InitializationAsync()
        {
            return FFmpeg.InitializationAsync();
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
                process.StartInfo.FileName = AppInit.Win32NT ? "data/ffprobe.exe" : File.Exists("data/ffprobe") ? "data/ffprobe" : "ffprobe";
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
