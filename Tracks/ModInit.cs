using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared;
using Shared.Engine;
using Shared.Models.Module;
using System;
using System.IO;
using System.Threading;
using Tracks.Engine;

namespace Tracks
{
    public class ModInit
    {
        public static bool IsInitialization { get; private set; }

        private static int _shutdownRegistered;
        private static int _shutdownTriggered;

        public static void loaded(InitspaceModel initspace)
        {
            RegisterShutdown(initspace);

            Directory.CreateDirectory("database/tracks");
            FFprobe.InitializationAsync().ContinueWith(t =>
            {
                IsInitialization = t.Result;
                TranscodingService.Instance.Configure(AppInit.conf.trackstranscoding);
            });
        }

        private static void RegisterShutdown(InitspaceModel initspace)
        {
            if (Interlocked.Exchange(ref _shutdownRegistered, 1) == 1)
                return;

            if (initspace?.app?.ApplicationServices != null)
            {
                var lifetime = initspace.app.ApplicationServices.GetService<IHostApplicationLifetime>();
                lifetime?.ApplicationStopping.Register(StopTranscoding);
            }

            AppDomain.CurrentDomain.ProcessExit += (_, _) => StopTranscoding();
        }

        private static void StopTranscoding()
        {
            if (Interlocked.Exchange(ref _shutdownTriggered, 1) == 1)
                return;

            try
            {
                TranscodingService.Instance.StopAll();
            }
            catch { }
        }
    }
}
