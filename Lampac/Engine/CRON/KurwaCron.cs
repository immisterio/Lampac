using Lampac.Engine.CORE;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Lampac.Engine.CRON
{
    public static class KurwaCron
    {
        async public static Task Run()
        {
            await Task.Delay(TimeSpan.FromMinutes(2)).ConfigureAwait(false);

            while (true)
            {
                try
                {
                    var externalids = await HttpClient.Get<Dictionary<string, string>>("http://bobr-kurwa.men/externalids.json", weblog: false).ConfigureAwait(false);
                    if (externalids != null && externalids.Count > 0)
                        await File.WriteAllTextAsync("cache/externalids/master.json", JsonConvert.SerializeObject(externalids)).ConfigureAwait(false);
                }
                catch { }

                await Task.Delay(TimeSpan.FromHours(5)).ConfigureAwait(false);
            }
        }
    }
}
