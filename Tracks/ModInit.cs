using Shared;
using Shared.Engine;
using System.IO;
using Tracks.Transcoding;

namespace Tracks
{
    public class ModInit
    {
        public static void loaded()
        {
            Directory.CreateDirectory("database/tracks");

            FFprobe.InitializationAsync().ConfigureAwait(false);

            try
            {
                TranscodingService.Instance.Configure(AppInit.conf.trackstranscoding);
            }
            catch
            {
            }
        }
    }
}
