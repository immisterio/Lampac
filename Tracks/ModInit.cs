using Shared;
using Shared.Engine;
using System.IO;
using Tracks.Engine;

namespace Tracks
{
    public class ModInit
    {
        public static bool IsInitialization { get; private set; }

        public static void loaded()
        {
            Directory.CreateDirectory("database/tracks");
            FFprobe.InitializationAsync().ContinueWith(t => 
            {
                IsInitialization = t.Result;
                TranscodingService.Instance.Configure(AppInit.conf.trackstranscoding);
            });
        }
    }
}
