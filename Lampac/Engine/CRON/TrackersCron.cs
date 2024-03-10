using Lampac.Engine.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Lampac.Engine.CRON
{
    public static class TrackersCron
    {
        async public static Task Run()
        {
            await Task.Delay(TimeSpan.FromMinutes(2));

            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(AppInit.conf.crontime.updateTrackers));
                
                try
                {
                    if (AppInit.modules == null || AppInit.modules.FirstOrDefault(i => i.dll == "DLNA.dll" && i.enable) == null)
                        continue;

                    if (AppInit.conf.dlna.enable && AppInit.conf.dlna.autoupdatetrackers)
                    {
                        HashSet<string> trackers = new HashSet<string>();

                        foreach (string uri in new string[] 
                        { 
                            "https://raw.githubusercontent.com/ngosang/trackerslist/master/trackers_all_ip.txt", 
                            "https://cdn.staticaly.com/gh/XIU2/TrackersListCollection/master/all.txt", 
                            "https://newtrackon.com/api/all" 
                        })
                        {
                            string plain = await HttpClient.Get(uri);
                            if (plain == null)
                                continue;

                            foreach (string line in plain.Split("\n"))
                            {
                                if (string.IsNullOrWhiteSpace(line) || line.Contains("["))
                                    continue;

                                if (line.StartsWith("http") || line.StartsWith("udp:"))
                                {
                                    try
                                    {
                                        var handler = new System.Net.Http.HttpClientHandler();
                                        handler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

                                        using (var client = new System.Net.Http.HttpClient(handler))
                                        {
                                            client.Timeout = TimeSpan.FromSeconds(2);

                                            await client.GetAsync(line.Trim().Replace("udp:", "http:"), System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                                            trackers.Add(line.Trim());
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }

                        if (trackers.Count > 0)
                            File.WriteAllLines("cache/trackers.txt", trackers);
                    }
                }
                catch { }
            }
        }
    }
}
