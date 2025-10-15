using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared;
using Shared.Engine;
using Shared.Models.Module;
using System;
using System.IO;
using Tracks.Engine;

namespace Tracks
{
    public class ModInit
    {
        public static bool IsInitialization { get; private set; }

        public static void loaded(InitspaceModel initspace)
        {
            RegisterShutdown(initspace);

            Directory.CreateDirectory("database/tracks");
            FFprobe.InitializationAsync().ContinueWith(t =>
            {
                IsInitialization = t.Result;
                TranscodingService.Instance.Configure(AppInit.conf.transcoding);
            });
        }

        static void RegisterShutdown(InitspaceModel initspace)
        {
            if (initspace?.app?.ApplicationServices != null)
            {
                var lifetime = initspace.app.ApplicationServices.GetService<IHostApplicationLifetime>();
                lifetime?.ApplicationStopping.Register(StopTranscoding);
            }

            AppDomain.CurrentDomain.ProcessExit += (_, _) => StopTranscoding();
        }

        static void StopTranscoding()
        {
            try
            {
                TranscodingService.Instance.StopAll();
            }
            catch { }
        }
    }
}
