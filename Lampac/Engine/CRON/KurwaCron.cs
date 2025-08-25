using Newtonsoft.Json;
using Shared.Engine;
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
                    var externalids = await Http.Get<Dictionary<string, string>>("http://bobr-kurwa.men/externalids.json", weblog: false).ConfigureAwait(false);
                    if (externalids != null && externalids.Count > 0)
                        await File.WriteAllTextAsync("data/externalids.json", JsonConvert.SerializeObject(externalids)).ConfigureAwait(false);
                }
                catch { }

                await Task.Delay(TimeSpan.FromHours(5)).ConfigureAwait(false);
            }
        }
    }
}
