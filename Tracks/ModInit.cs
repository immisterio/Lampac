using Shared.Engine;
using System.Threading;

namespace Tracks
{
    public class ModInit
    {
        public static bool Initialization;

        public static void loaded()
        {
            ThreadPool.QueueUserWorkItem(async _ =>
            {
                Initialization = await FFprobe.InitializationAsync();
            });
        }
    }
}
