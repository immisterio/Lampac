using Shared;
using Shared.Engine;
using Shared.Models;
using System;
using System.Threading;

namespace Lampac.Engine.CRON
{
    public static class SyncCron
    {
        public static void Run()
        {
            _cronTimer = new Timer(cron, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
        }

        static Timer _cronTimer;

        static bool _cronWork = false;

        async static void cron(object state)
        {
            if (_cronWork)
                return;

            _cronWork = true;

            try
            {
                var sync = AppInit.conf?.sync;

                if (sync == null || !sync.enable || sync.type != "slave" || string.IsNullOrEmpty(sync.api_host) || string.IsNullOrEmpty(sync.api_passwd))
                    return;

                var init = await Http.Get<AppInit>(sync.api_host + "/api/sync", timeoutSeconds: 5, headers: HeadersModel.Init("localrequest", sync.api_passwd), weblog: false);
                if (init != null)
                {
                    if (sync.sync_full)
                    {
                        init.sync = sync;
                        AppInit.conf = init;
                    }
                    else
                    {
                        AppInit.conf.accsdb.users = init.accsdb.users;
                    }
                }
            }
            catch { }
            finally
            {
                _cronWork = false;
            }
        }
    }
}
