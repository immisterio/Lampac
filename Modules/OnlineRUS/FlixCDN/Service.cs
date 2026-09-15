using Shared.Models.Online.Settings;
using Shared.Models.Templates;
using Shared.Services;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FlixCDN;

public struct FlixCDNInvoke
{
    OnlinesSettings init;
    HttpHydra httpHydra;
    Func<string, string> onstreamfile;

    static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FlixCDNInvoke(OnlinesSettings init, HttpHydra httpHydra, Func<string, string> onstreamfile)
    {
        this.init = init;
        this.httpHydra = httpHydra;
        this.onstreamfile = onstreamfile;
    }


    public string BuildPlayerUrl(long kinopoisk_id)
    {
        return $"{init.host}/show/kinopoisk/{kinopoisk_id}?extrans=1&extepi=1&unfseason=1";
    }


    async public Task<PlayerPayload> GetPlayer(long kinopoisk_id)
    {
        if (kinopoisk_id <= 0)
            return null;

        string html = await httpHydra.Get(BuildPlayerUrl(kinopoisk_id), safety: true);
        return ParsePlayer(html);
    }


    static PlayerPayload ParsePlayer(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        const string marker = "window.__PLAYER_PAYLOAD__ = ";
        int start = html.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            return null;

        start += marker.Length;
        int end = html.IndexOf(';', start);
        if (end < 0)
            return null;

        string json = html.Substring(start, end - start).Trim();
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<PlayerPayload>(json, jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public StreamQualityTpl GetStreamQualityTpl(string file)
    {
        var streamquality = new StreamQualityTpl();
        var qualityByLink = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var linkOrder = new List<string>();

        foreach (Match m in Regex.Matches(file ?? string.Empty, "\\[(?<q>\\d{3,4})\\](?<url>https?://[^,\"\\[\\s]+)"))
        {
            string link = m.Groups["url"].Value;
            if (string.IsNullOrEmpty(link) || !int.TryParse(m.Groups["q"].Value, out int quality))
                continue;

            if (qualityByLink.TryGetValue(link, out int currentQuality))
            {
                if (quality > currentQuality)
                {
                    string restoredLink = RestoreQualityLink(link, currentQuality, quality);
                    if (!string.Equals(restoredLink, link, StringComparison.OrdinalIgnoreCase) && !qualityByLink.ContainsKey(restoredLink))
                    {
                        qualityByLink[restoredLink] = quality;
                        linkOrder.Add(restoredLink);
                    }
                }
                else if (quality < currentQuality)
                {
                    qualityByLink[link] = quality;
                }

                continue;
            }

            qualityByLink[link] = quality;
            linkOrder.Add(link);
        }

        foreach (string link in linkOrder)
            streamquality.Insert(onstreamfile.Invoke(link), $"{qualityByLink[link]}p");

        if (!streamquality.IsEmpty)
            return streamquality;

        foreach (Match m in Regex.Matches(file ?? string.Empty, "(https?://[^,\"\\[\\s]+\\.(m3u8|mp4)(:hls:manifest\\.m3u8)?)"))
        {
            string link = m.Groups[1].Value;
            if (string.IsNullOrEmpty(link))
                continue;

            streamquality.Append(onstreamfile.Invoke(link), "auto");
            break;
        }

        return streamquality;
    }


    static string RestoreQualityLink(string link, int sourceQuality, int targetQuality)
    {
        if (string.IsNullOrEmpty(link) || sourceQuality <= 0 || targetQuality <= sourceQuality)
            return link;

        string source = $"/{sourceQuality}.mp4";
        int index = link.IndexOf(source, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return link;

        return link.Substring(0, index) + $"/{targetQuality}.mp4" + link.Substring(index + source.Length);
    }
}
