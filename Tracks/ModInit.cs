using Shared.Engine;
using System.IO;

namespace Tracks
{
    public class ModInit
    {
        public static void loaded()
        {
            Directory.CreateDirectory("cache/tracks");

            FFprobe.InitializationAsync().ConfigureAwait(false);
        }
    }
}
