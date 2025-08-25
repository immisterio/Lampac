using Shared.Engine;
using System.IO;

namespace Tracks
{
    public class ModInit
    {
        public static void loaded()
        {
            Directory.CreateDirectory("database/tracks");

            FFprobe.InitializationAsync().ConfigureAwait(false);
        }
    }
}
