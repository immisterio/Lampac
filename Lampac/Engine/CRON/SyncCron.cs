using Lampac.Engine.CORE;
using Shared.Model.Online;
using System;
using System.Threading.Tasks;

namespace Lampac.Engine.CRON
{
    public static class SyncCron
    {
        async public static Task Run()
        {
            await Task.Delay(TimeSpan.FromSeconds(20)).ConfigureAwait(false);

            while (true)
            {
                var sync = AppInit.conf?.sync;

                if (sync == null || !sync.enable || sync.type != "slave" || string.IsNullOrEmpty(sync.api_host) || string.IsNullOrEmpty(sync.api_passwd))
                {
                    await Task.Delay(TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                    continue;
                }

                await Task.Delay(TimeSpan.FromSeconds(1));

                try
                {
                    var init = await HttpClient.Get<AppInit>(sync.api_host + "/api/sync", timeoutSeconds: 5, headers: HeadersModel.Init("localrequest", sync.api_passwd));
                    if (init != null)
                    {
                        if (sync.sync_full)
                        {
                            init.sync = sync;
                            AppInit.cacheconf.Item1 = init;
                        }
                        else
                        {
                            AppInit.conf.accsdb.users = init.accsdb.users;
                        }
                    }
                }
                catch { }
            }
        }
    }
}
