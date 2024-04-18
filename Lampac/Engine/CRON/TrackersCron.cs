using Lampac.Engine.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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
                try
                {
                    if (AppInit.modules == null || AppInit.modules.FirstOrDefault(i => i.dll == "DLNA.dll" && i.enable) == null)
                    {
                        await Task.Delay(TimeSpan.FromMinutes(1));
                        continue;
                    }

                    if (AppInit.conf.dlna.enable && AppInit.conf.dlna.autoupdatetrackers)
                    {
                        HashSet<string> trackers = new HashSet<string>(), trackers_bad = new HashSet<string>(), temp = new HashSet<string>();

                        foreach (string uri in new string[]
                        {
                            "http://redapi.cfhttp.top/trackers.txt",
                            "https://raw.githubusercontent.com/ngosang/trackerslist/master/trackers_all_ip.txt",
                            "https://raw.githubusercontent.com/XIU2/TrackersListCollection/master/all.txt",
                            "https://newtrackon.com/api/all"
                        })
                        {
                            string plain = await HttpClient.Get(uri);
                            if (plain == null)
                                continue;

                            foreach (string line in plain.Replace("\r", "").Replace("\t", "").Split("\n"))
                                if (!string.IsNullOrEmpty(line))
                                    temp.Add(line.Trim());
                        }

                        foreach (string url in temp)
                        {
                            if (await ckeck(url))
                                trackers.Add(url);
                            else
                                trackers_bad.Add(url);
                        }

                        File.WriteAllLines("cache/trackers_bad.txt", trackers_bad);
                        File.WriteAllLines("cache/trackers.txt", trackers.OrderByDescending(i => Regex.IsMatch(i, "[0-9]+\\.[0-9]+\\.[0-9]+\\.[0-9]+")).ThenByDescending(i => i.StartsWith("http")));
                    }
                }
                catch { }

                await Task.Delay(TimeSpan.FromMinutes(AppInit.conf.dlna.intervalUpdateTrackers));
            }
        }


        async static ValueTask<bool> ckeck(string tracker)
        {
            if (string.IsNullOrWhiteSpace(tracker) || tracker.Contains("["))
                return false;

            if (tracker.StartsWith("http"))
            {
                try
                {
                    var handler = new System.Net.Http.HttpClientHandler();
                    handler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

                    using (var client = new System.Net.Http.HttpClient(handler))
                    {
                        client.Timeout = TimeSpan.FromSeconds(7);
                        await client.GetAsync(tracker, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                        return true;
                    }
                }
                catch { }
            }
            else if (tracker.StartsWith("udp:"))
            {
                try
                {
                    tracker = tracker.Replace("udp://", "");

                    string host = tracker.Split(':')[0].Split('/')[0];
                    int port = tracker.Contains(":") ? int.Parse(tracker.Split(':')[1].Split('/')[0]) : 6969;

                    using (UdpClient client = new UdpClient(host, port))
                    {
                        CancellationTokenSource cts = new CancellationTokenSource();
                        cts.CancelAfter(7000);

                        string uri = Regex.Match(tracker, "^[^/]/(.*)").Groups[1].Value;
                        await client.SendAsync(Encoding.UTF8.GetBytes($"GET /{uri} HTTP/1.1\r\nHost: {host}\r\n\r\n"), cts.Token);
                        return true;
                    }
                }
                catch { }
            }

            return false;
        }
    }
}
