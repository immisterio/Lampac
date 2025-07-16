using System.Diagnostics;

namespace Shared.Engine
{
    public static class Bash
    {
        public static bool Invoke(string comand)
        {
            try
            {
                var processInfo = new ProcessStartInfo();
                processInfo.UseShellExecute = false;
                processInfo.RedirectStandardOutput = true;
                processInfo.FileName = "/bin/bash";
                processInfo.Arguments = $" -c \"{comand.Replace("\"", "\\\"").Replace("'", "\\\'")}\"";

                var process = Process.Start(processInfo);
                if (process == null)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        async public static Task<string> Run(string comand)
        {
            try
            {
                var processInfo = new ProcessStartInfo();
                processInfo.UseShellExecute = false;
                processInfo.RedirectStandardError = true;
                processInfo.RedirectStandardOutput = true;
                processInfo.FileName = "/bin/bash";
                processInfo.Arguments = $" -c \"{comand.Replace("\"", "\\\"").Replace("'", "\\\'")}\"";

                var process = Process.Start(processInfo);
                if (process == null)
                    return null;

                string outPut = await process.StandardOutput.ReadToEndAsync();
                outPut += await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                return outPut;
            }
            catch
            {
                return null;
            }
        }
    }
}
