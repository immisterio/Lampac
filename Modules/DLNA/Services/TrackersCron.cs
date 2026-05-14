using Shared.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DLNA;

public static class TrackersCron
{
    static readonly Serilog.ILogger Log = Serilog.Log.ForContext("SourceContext", nameof(TrackersCron));

    public static void Start()
    {
        _cronTimer = new Timer(cron, null, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(ModInit.conf.intervalUpdateTrackers));
    }

    public static void Stop()
    {
        _cronTimer.Dispose();
    }

    static Timer _cronTimer;

    static int _updatingDb = 0;

    async static void cron(object state)
    {
        if (Interlocked.Exchange(ref _updatingDb, 1) == 1)
            return;

        try
        {
            if (ModInit.conf.autoupdatetrackers)
            {
                var trackers = new HashSet<string>();
                var trackers_bad = new HashSet<string>();
                var temp = new HashSet<string>();

                foreach (string uri in new string[]
                {
                    "https://raw.githubusercontent.com/ngosang/trackerslist/master/trackers_all_ip.txt",
                    "https://raw.githubusercontent.com/XIU2/TrackersListCollection/master/all.txt",
                    "https://newtrackon.com/api/all"
                })
                {
                    await Http.GetSpan(uri, plain =>
                    {
                        foreach (var r in plain.Split("\n"))
                        {
                            ReadOnlySpan<char> line = plain[r];
                            line = line.Trim('\r').Trim('\t').Trim();
                            if (line.IsEmpty)
                                continue;

                            temp.Add(line.ToString());
                        }
                    });
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
        catch (System.Exception ex)
        {
            Log.Error(ex, "CatchId={CatchId}", "id_w8gwnxlr");
        }
        finally
        {
            Volatile.Write(ref _updatingDb, 0);
        }
    }


    async static Task<bool> ckeck(string tracker)
    {
        if (string.IsNullOrWhiteSpace(tracker) || tracker.Contains("["))
            return false;

        if (tracker.StartsWith("http"))
        {
            try
            {
                using (var handler = new System.Net.Http.HttpClientHandler())
                {
                    handler.ServerCertificateCustomValidationCallback = Http.AlwaysAllowCertificate;

                    using (var client = new System.Net.Http.HttpClient(handler))
                    {
                        client.Timeout = TimeSpan.FromSeconds(7);
                        await client.GetAsync(tracker, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                        return true;
                    }
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
