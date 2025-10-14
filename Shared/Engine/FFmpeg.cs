using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace Shared.Engine
{
    /// <summary>
    /// https://github.com/BtbN/FFmpeg-Builds/releases
    /// </summary>
    public static class FFmpeg
    {
        #region InitializationAsync
        static bool disableInstall = false;

        async public static Task<bool> InitializationAsync()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                #region Windows
                if (File.Exists("data/ffmpeg.exe") && File.Exists("data/ffprobe.exe"))
                {
                    Console.WriteLine("FFmpeg: Initialization");
                    return true;
                }

                if (RuntimeInformation.ProcessArchitecture != Architecture.X64 && RuntimeInformation.ProcessArchitecture != Architecture.Arm64)
                {
                    Console.WriteLine("FFmpeg: Architecture unknown");
                    return false;
                }

                if (disableInstall)
                    return true;

                disableInstall = true;
                string arh = RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "64" : "arm64";

                if (await Http.DownloadFile($"https://github.com/immisterio/ffmpeg/releases/download/ffmpeg2/ffmpeg-master-latest-win{arh}-gpl.zip", "data/ffmpeg.zip"))
                {
                    ZipFile.ExtractToDirectory("data/ffmpeg.zip", "data/", overwriteFiles: true);

                    File.Delete("data/ffmpeg.zip");

                    Console.WriteLine("FFmpeg: Initialization");
                    disableInstall = false;
                    return true;
                }

                Console.WriteLine($"FFmpeg: error download ffmpeg-win{arh}-gpl.zip");
                disableInstall = false;
                return false;
                #endregion
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                #region Linux
                if (File.Exists("data/ffmpeg") || File.Exists("/bin/ffmpeg"))
                {
                    Console.WriteLine("FFmpeg: Initialization");
                    return true;
                }

                string version = await Bash.Run("ffmpeg -version");
                if (version == null || !version.Contains("FFmpeg developers"))
                {
                    if (disableInstall)
                        return true;

                    disableInstall = true;

                    if (RuntimeInformation.ProcessArchitecture == Architecture.X64 || RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                    {
                        string arh = RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "64" : "arm64";
                        if (await Http.DownloadFile($"https://github.com/immisterio/ffmpeg/releases/download/ffmpeg2/ffmpeg-master-latest-linux{arh}-gpl.zip", "data/ffmpeg"))
                        {
                            ZipFile.ExtractToDirectory("data/ffmpeg.zip", "data/", overwriteFiles: true);

                            File.Delete("data/ffmpeg.zip");

                            Bash.Invoke($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), "data/ffmpeg")}");
                            Bash.Invoke($"chmod +x {Path.Join(Directory.GetCurrentDirectory(), "data/ffprobe")}");

                            await Task.Delay(2000); // Wait for chmod to complete
                            Console.WriteLine("FFmpeg: Initialization");
                            disableInstall = false;
                            return true;
                        }
                    }
                    else
                    {
                        await Bash.Run("apt update && apt install -y ffmpeg");
                        version = await Bash.Run("ffmpeg -version");
                        if (version == null || !version.Contains("FFmpeg developers"))
                        {
                            Console.WriteLine("FFmpeg: error install ffmpeg");
                            disableInstall = false;
                            return false;
                        }
                    }
                }

                Console.WriteLine("FFmpeg: Initialization");
                return true;
                #endregion
            }

            Console.WriteLine("FFmpeg: OS unknown");
            return false;
        }
        #endregion

        #region RunAsync
        async public static ValueTask<(string outputData, string errorData)> RunAsync(string comand, string workingDirectory = null, ProcessPriorityClass? priorityClass = null)
        {
            try
            {
                var process = new Process();
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.FileName = AppInit.Win32NT ? "data/ffmpeg.exe" : File.Exists("data/ffmpeg") ? "data/ffmpeg" : "ffmpeg";
                process.StartInfo.Arguments = comand;
                process.StartInfo.WorkingDirectory = workingDirectory;
                process.Start();

                if (priorityClass != null)
                {
                    try
                    {
                        process.PriorityClass = (ProcessPriorityClass)priorityClass;
                    }
                    catch (InvalidOperationException)
                    {
                        // Процесс завершился до установки приоритета
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine("FFmpeg: " + ex.Message);
                    }
                }

                string outputData = string.Empty, errorData = string.Empty;

                process.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                        outputData += args.Data + "\n";
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                        errorData += args.Data + "\n";
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
