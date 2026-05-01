using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Attributes;
using Shared.Models.Base;
using Shared.Models.Templates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Mikai;

public class MikaiController : BaseOnlineController
{
    public MikaiController() : base(ModInit.conf) { }

    [HttpGet]
    [Staticache]
    [Route("lite/mikai")]
    async public Task<ActionResult> Index(string title, string original_title, int year, int animeid = 0, int t = -1, bool rjson = false)
    {
        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        string enc_title = HttpUtility.UrlEncode(title);
        string enc_original_title = HttpUtility.UrlEncode(original_title);

        #region search
        if (animeid == 0)
        {
            if (string.IsNullOrWhiteSpace(title))
                return OnError();

            rhubFallbackSearch:
            var search = await InvokeCacheResult<List<(int id, string title, string year, string poster)>>($"mikai:search:{title}", TimeSpan.FromHours(4), async e =>
            {
                var root = await httpHydra.Get<SearchResponse>($"{init.host}/v1/anime/search?page=1&limit=24&sort=year&order=desc&name={HttpUtility.UrlEncode(title)}");
                var items = root?.result;
                if (items == null || items.Length == 0)
                    return e.Fail("search", refresh_proxy: true);

                var list = new List<(int id, string title, string year, string poster)>();

                foreach (var item in items)
                {
                    if (item?.id == 0)
                        continue;

                    string itemTitle = item?.details?.names?.name ?? item?.details?.names?.nameEnglish ?? item?.details?.names?.nameNative ?? item?.slug;
                    if (string.IsNullOrWhiteSpace(itemTitle))
                        continue;

                    string poster = string.Empty;
                    if (!string.IsNullOrWhiteSpace(item?.media?.posterUid))
                        poster = PosterApi.Size($"https://images.mikai.me/poster/small/{item.media.posterUid}.webp");

                    list.Add((item.id, itemTitle, item.year > 0 ? item.year.ToString() : string.Empty, poster));
                }

                if (list.Count == 0)
                    return e.Fail("catalog");

                return e.Success(list);
            });

            if (IsRhubFallback(search))
                goto rhubFallbackSearch;

            if (!search.IsSuccess)
                return OnError(search.ErrorMsg);

            var stpl = new SimilarTpl(search.Value.Count);

            foreach (var item in search.Value)
            {
                stpl.Append(
                    item.title,
                    item.year,
                    string.Empty,
                    $"{host}/lite/mikai?rjson={rjson}&title={enc_title}&original_title={enc_original_title}&year={year}&animeid={item.id}",
                    item.poster
                );
            }

            return ContentTpl(stpl);
        }
    #endregion

    rhubFallbackAnime:
        var anime = await InvokeCacheResult<AnimeResult>($"mikai:anime:{animeid}", TimeSpan.FromHours(2), async e =>
        {
            var root = await httpHydra.Get<AnimeResponse>($"{init.host}/v1/anime/{animeid}");
            var result = root?.result;
            if (result?.players == null || result.players.Length == 0)
                return e.Fail("players", refresh_proxy: true);

            return e.Success(result);
        });

        if (IsRhubFallback(anime))
            goto rhubFallbackAnime;

        if (!anime.IsSuccess)
            return OnError(anime.ErrorMsg);

        var voices = new List<(string title, List<ProviderEpisode> episodes)>();
        foreach (var player in anime.Value.players)
        {
            var provider = player?.providers?.FirstOrDefault(p => string.Equals(p?.name, "ASHDI", StringComparison.OrdinalIgnoreCase));
            if (provider?.episodes == null || provider.episodes.Length == 0)
                continue;

            string voiceTitle = player?.team?.name;
            if (string.IsNullOrWhiteSpace(voiceTitle))
                voiceTitle = "ASHDI";

            voices.Add((voiceTitle, provider.episodes.Where(e => !string.IsNullOrWhiteSpace(e?.playLink)).ToList()));
        }

        voices = voices.Where(v => v.episodes.Count > 0).ToList();
        if (voices.Count == 0)
            return OnError("ashdi providers");

        if (t == -1)
            t = 0;

        if (t >= voices.Count)
            t = 0;

        var currentVoice = voices[t];

        var vtpl = new VoiceTpl(voices.Count);
        for (int i = 0; i < voices.Count; i++)
        {
            vtpl.Append(
                voices[i].title,
                i == t,
                $"{host}/lite/mikai?rjson={rjson}&title={enc_title}&original_title={enc_original_title}&year={year}&animeid={animeid}&t={i}"
            );
        }

        var episodes = currentVoice.episodes
            .OrderBy(e => e.number)
            .ToList();

        if (episodes.Count == 0)
            return OnError("episodes");

        var etpl = new EpisodeTpl(vtpl, episodes.Count);
        string season = DetectSeason(anime.Value);

        foreach (var item in episodes)
        {
            int episodeNum = item.number > 0 ? item.number : episodes.IndexOf(item) + 1;
            string episodeTitle = $"{episodeNum} серия";
            string link = accsArgs($"{host}/lite/ashdi/vod.m3u8?uri={EncryptQuery(item.playLink)}");

            etpl.Append(
                episodeTitle,
                title ?? original_title,
                season,
                episodeNum.ToString(),
                link,
                "call",
                streamlink: $"{link}&play=true",
                vast: init.vast
            );
        }

        return ContentTpl(etpl);
    }


    static string DetectSeason(AnimeResult anime)
    {
        if (anime == null)
            return "1";

        var seasonSource = new[]
        {
            anime.slug,
            anime.details?.names?.name,
            anime.details?.names?.nameNative,
            anime.details?.names?.nameEnglish
        };

        foreach (var source in seasonSource)
        {
            if (string.IsNullOrWhiteSpace(source))
                continue;

            var match = Regex.Match(source, @"(?<!\d)(\d{1,2})\s*[- ]?(?:сезон|sezon|season)\b", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value;
        }

        return "1";
    }
}
