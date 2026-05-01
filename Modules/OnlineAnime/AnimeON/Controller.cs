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

namespace AnimeON;

public class AnimeONController : BaseOnlineController
{
    public AnimeONController() : base(ModInit.conf) { }

    [HttpGet]
    [Staticache]
    [Route("lite/animeon")]
    async public Task<ActionResult> Index(string imdb_id, string title, string original_title, int year, int t = -1, int s = -1, int animeid = 0, bool rjson = false)
    {
        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        string enc_title = HttpUtility.UrlEncode(title);
        string enc_original_title = HttpUtility.UrlEncode(original_title);

        #region search
        if (animeid == 0)
        {
        rhubFallbackSearch:
            var search = await InvokeCacheResult<List<CatalogItem>>($"animeon:search:{title}", TimeSpan.FromHours(4), async e =>
            {
                var root = await httpHydra.Get<SearchResponse>($"{init.host}/api/anime/search?text={HttpUtility.UrlEncode(title)}");
                if (root == null)
                    return e.Fail("search", refresh_proxy: true);

                var result = root?.result;
                if (result == null || result.Length == 0)
                    return e.Fail("result");

                var catalog = new List<CatalogItem>(result.Length);

                foreach (var node in result)
                {
                    int id = node?.id ?? 0;
                    if (id == 0)
                        continue;

                    catalog.Add(new CatalogItem
                    {
                        Id = id,
                        Season = node?.season ?? 0,
                        ImdbId = node?.imdbId,
                        Title = node?.titleUa ?? node?.titleEn,
                        Year = node?.releaseDate,
                        Poster = node?.image?.preview
                    });
                }

                if (catalog.Count == 0)
                    return e.Fail("catalog");

                return e.Success(catalog);
            });

            if (IsRhubFallback(search))
                goto rhubFallbackSearch;

            if (!search.IsSuccess)
                return OnError(search.ErrorMsg);

            if (search.Value.Count == 1 && !string.IsNullOrWhiteSpace(imdb_id) && string.Equals(search.Value[0].ImdbId, imdb_id, StringComparison.OrdinalIgnoreCase))
                return LocalRedirect($"{host}/lite/animeon?rjson={rjson}&s={search.Value[0].Season}&imdb_id={HttpUtility.UrlEncode(imdb_id)}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&year={year}&animeid={search.Value[0].Id}");

            var stpl = new SimilarTpl(search.Value.Count);

            foreach (var item in search.Value)
            {
                stpl.Append(
                    item.Title,
                    item.Year,
                    string.Empty,
                    $"{host}/lite/animeon?rjson={rjson}&s={item.Season}&imdb_id={HttpUtility.UrlEncode(imdb_id)}&title={enc_title}&original_title={enc_original_title}&year={year}&animeid={item.Id}",
                    PosterApi.Size($"{init.host}/api/uploads/images/{item.Poster}")
                );
            }

            return ContentTpl(stpl);
        }
    #endregion

    rhubFallbackTranslations:
        var translations = await InvokeCacheResult<List<TranslationOption>>($"animeon:translations:{animeid}", TimeSpan.FromHours(2), async e =>
        {
            var root = await httpHydra.Get<TranslationsResponse>($"{init.host}/api/player/{animeid}/translations");
            var items = root?.translations;
            if (items == null || items.Length == 0)
                return e.Fail("translations");

            var list = new List<TranslationOption>();

            foreach (var node in items)
            {
                int translationId = node?.translation?.id ?? 0;
                if (translationId == 0)
                    continue;

                int playerId = node?.player?
                    .FirstOrDefault(p => string.Equals(p?.name, "Ashdi", StringComparison.OrdinalIgnoreCase))?.id ?? 0;
                if (playerId == 0)
                    continue;

                string voiceTitle = node?.translation?.name ?? "Ashdi";
                list.Add(new TranslationOption
                {
                    TranslationId = translationId,
                    PlayerId = playerId,
                    Title = voiceTitle
                });
            }

            if (list.Count == 0)
                return e.Fail("ashdi translations");

            return e.Success(list);
        });

        if (IsRhubFallback(translations))
            goto rhubFallbackTranslations;

        if (!translations.IsSuccess)
            return OnError(translations.ErrorMsg);

        if (t == -1)
            t = 0;

        if (t >= translations.Value.Count)
            t = 0;

        var currentTranslation = translations.Value[t];

    rhubFallbackEpisodes:
        var episodes = await InvokeCacheResult<List<EpisodeOption>>($"animeon:episodes:{animeid}:{currentTranslation.PlayerId}:{currentTranslation.TranslationId}", TimeSpan.FromMinutes(40), async e =>
        {
            var root = await httpHydra.Get<EpisodesResponse>($"{init.host}/api/player/{animeid}/episodes?take=100&skip=-1&playerId={currentTranslation.PlayerId}&translationId={currentTranslation.TranslationId}");
            var items = root?.episodes;
            if (items == null || items.Length == 0)
                return e.Fail("episodes");

            var list = new List<EpisodeOption>();

            foreach (var node in items)
            {
                string file = node?.fileUrl;
                if (string.IsNullOrWhiteSpace(file))
                    continue;

                int episode = node?.episode ?? 0;
                string epTitle = episode > 0 ? $"{episode} серия" : "Серия";

                list.Add(new EpisodeOption
                {
                    Episode = episode,
                    Title = epTitle,
                    FileUrl = file
                });
            }

            if (list.Count == 0)
                return e.Fail("episode list");

            return e.Success(list);
        });

        if (IsRhubFallback(episodes))
            goto rhubFallbackEpisodes;

        if (!episodes.IsSuccess)
            return OnError(episodes.ErrorMsg);

        var vtpl = new VoiceTpl(translations.Value.Count);
        for (int i = 0; i < translations.Value.Count; i++)
        {
            var voice = translations.Value[i];
            vtpl.Append(
                voice.Title,
                i == t,
                $"{host}/lite/animeon?rjson={rjson}&imdb_id={HttpUtility.UrlEncode(imdb_id)}&title={enc_title}&original_title={enc_original_title}&year={year}&animeid={animeid}&s={s}&t={i}"
            );
        }

        var etpl = new EpisodeTpl(vtpl, episodes.Value.Count);
        string sArch = s.ToString();

        foreach (var item in episodes.Value)
        {
            string stream = HostStreamProxy(item.FileUrl);
            string epNum = item.Episode > 0 ? item.Episode.ToString() : Regex.Match(item.Title, "([0-9]+)").Groups[1].Value;
            if (string.IsNullOrEmpty(epNum))
                epNum = (episodes.Value.IndexOf(item) + 1).ToString();

            etpl.Append(
                item.Title,
                title ?? original_title,
                sArch,
                epNum,
                stream,
                vast: init.vast
            );
        }

        return ContentTpl(etpl);
    }
}
