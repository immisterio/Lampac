using Newtonsoft.Json;
using Shared.Engine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Lampac.Engine.CRON
{
    public static class KurwaCron
    {
        public static void Run()
        {
            _cronTimer = new Timer(cron, null, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5));
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
                var externalids = await Http.Get<Dictionary<string, string>>("http://bobr-kurwa.men/externalids.json", weblog: false).ConfigureAwait(false);
                if (externalids != null && externalids.Count > 0)
                    await File.WriteAllTextAsync("data/externalids.json", JsonConvert.SerializeObject(externalids)).ConfigureAwait(false);
            }
            finally
            {
                _cronWork = false;
            }
        }
    }
}
