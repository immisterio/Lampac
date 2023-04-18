using Lampac;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TorrServer
{
    public class ModInit
    {
        public static DateTime lastActve = default;

        public static HashSet<string> clientIps = new HashSet<string>();

        public static System.Diagnostics.Process tsprocess;

        public static void loaded()
        {
            bool autoinstall = false;

            if (AppInit.conf.ts.tsport == 0)
                AppInit.conf.ts.tsport = new Random().Next(7000, 7400);

            if (string.IsNullOrWhiteSpace(AppInit.conf.ts.tspath))
            {
                autoinstall = true;
                AppInit.conf.ts.tspath = "torrserver/TorrServer-linux";
            }


            ThreadPool.QueueUserWorkItem(async _ => 
            {
                if (autoinstall)
                {
                    // auto install
                }

                while (true)
                {
                    await Task.Delay(TimeSpan.FromMinutes(2));
                    if (lastActve == default || !AppInit.conf.ts.enable)
                        continue;

                    try
                    {
                        if (lastActve > DateTime.Now.AddMinutes(-20))
                            continue;

                        tsprocess?.Dispose();
                        tsprocess = null;

                        lastActve = default;
                        clientIps.Clear();
                    }
                    catch { }
                }
            });
        }
    }
}
