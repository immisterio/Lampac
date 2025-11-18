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
            _cronTimer = new Timer(cron, null, TimeSpan.FromMinutes(2), TimeSpan.FromHours(1));
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
                var externalids = await Http.Get<Dictionary<string, string>>("http://194.246.82.144/externalids.json", weblog: false);
                if (externalids != null && externalids.Count > 0)
                    await File.WriteAllTextAsync("data/externalids.json", JsonConvert.SerializeObject(externalids));

                var cdnmovies = await Http.Download("http://194.246.82.144/externalids.json");
                if (cdnmovies != null && cdnmovies.Length > 0)
                    await File.WriteAllBytesAsync("data/cdnmovies.json", cdnmovies);

                var veoveo = await Http.Download("http://194.246.82.144/veoveo.json");
                if (veoveo != null && veoveo.Length > 0)
                    await File.WriteAllBytesAsync("data/veoveo.json", veoveo);

                var kodik = await Http.Download("http://194.246.82.144/kodik.json");
                if (kodik != null && kodik.Length > 0)
                    await File.WriteAllBytesAsync("data/kodik.json", kodik);
            }
            catch { }
            finally
            {
                _cronWork = false;
            }
        }
    }
}
