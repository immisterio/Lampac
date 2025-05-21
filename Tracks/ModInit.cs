using Shared.Engine;
using System.IO;
using System.Threading;

namespace Tracks
{
    public class ModInit
    {
        public static bool Initialization;

        public static void loaded()
        {
            Directory.CreateDirectory("cache/tracks");

            ThreadPool.QueueUserWorkItem(async _ =>
            {
                Initialization = await FFprobe.InitializationAsync();
            });
        }
    }
}
