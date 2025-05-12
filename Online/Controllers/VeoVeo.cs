﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Lampac.Engine.CORE;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;
using System.Web;
using Lampac.Models.LITE;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using Shared.Engine;
using Shared.Model.Online.VeoVeo;
using System.Text.RegularExpressions;

namespace Lampac.Controllers.LITE
{
    public class VeoVeo : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/veoveo")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int s = -1, bool rjson = false, bool origsource = false)
        {
            var init = await loadKit(AppInit.conf.VeoVeo);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var movie = search(init, proxyManager, proxy, imdb_id, kinopoisk_id, title, original_title);
            if (movie == null)
                return OnError();

            #region media
            var cache = await InvokeCache<JArray>($"{init.plugin}:{movie.id}", cacheTime(20, init: init), proxyManager, async res =>
            {
                string uri = $"{init.host}/balancer-api/proxy/playlists/catalog-api/episodes?content-id={movie.id}";
                var root = await HttpClient.Get<JArray>(init.cors(uri), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));

                if (root == null || root.Count == 0)
                    return res.Fail("data");

                return root;
            });
            #endregion

            return OnResult(cache, () =>
            {
                if (cache.Value.First["season"].Value<int>("order") == 0)
                {
                    #region Фильм
                    var mtpl = new MovieTpl(title, original_title);

                    string stream = HostStreamProxy(init, cache.Value.First.Value<string>("m3u8MasterFilePath"), proxy: proxy);

                    mtpl.Append(title ?? original_title, stream, vast: init.vast);

                    return rjson ? mtpl.ToJson() : mtpl.ToHtml();
                    #endregion
                }
                else
                {
                    #region Сериал
                    if (s == -1)
                    {
                        var tpl = new SeasonTpl();
                        var hash = new HashSet<int>();

                        foreach (var item in cache.Value)
                        {
                            var season = item["season"].Value<int>("order");
                            if (hash.Contains(season))
                                continue;

                            hash.Add(season);
                            string link = $"{host}/lite/veoveo?rjson={rjson}&kinopoisk_id={kinopoisk_id}&imdb_id={imdb_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={season}";
                            tpl.Append($"{season} сезон", link, season);
                        }

                        return rjson ? tpl.ToJson() : tpl.ToHtml();
                    }
                    else
                    {
                        var etpl = new EpisodeTpl();

                        foreach (var episode in cache.Value.Where(i => i["season"].Value<int>("order") == s))
                        {
                            string name = episode.Value<string>("title");
                            string file = episode.Value<string>("m3u8MasterFilePath");

                            if (string.IsNullOrEmpty(file))
                                continue;

                            string stream = HostStreamProxy(init, file, proxy: proxy);
                            etpl.Append(name ?? $"{episode.Value<int>("order")} серия", title ?? original_title, s.ToString(), episode.Value<int>("order").ToString(), stream, vast: init.vast);
                        }

                        return rjson ? etpl.ToJson() : etpl.ToHtml();
                    }
                    #endregion
                }

            }, origsource: origsource);
        }


        #region search
        public static List<Movie> database = null;

        Movie search(OnlinesSettings init, ProxyManager proxyManager, WebProxy proxy, string imdb_id, long kinopoisk_id, string title, string original_title)
        {
            if (database == null)
                database = JsonHelper.ListReader<Movie>("data/veoveo.json", 45000);

            string NormalizeString(string str) => Regex.Replace(str?.ToLower() ?? "", "[^a-zа-я0-9]", "");

            Movie goSearch(bool searchToId)
            {
                foreach (var item in database)
                {
                    if (searchToId)
                    {
                        if (kinopoisk_id > 0)
                        {
                            if (item.kinopoiskId == kinopoisk_id)
                                return item;
                        }

                        if (!string.IsNullOrEmpty(imdb_id))
                        {
                            if (item.imdbId == imdb_id)
                                return item;
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(original_title))
                        {
                            if (NormalizeString(item.originalTitle) == NormalizeString(original_title))
                                return item;
                        }

                        if (!string.IsNullOrEmpty(title))
                        {
                            if (NormalizeString(item.title) == NormalizeString(title))
                                return item;
                        }
                    }
                }

                return null;
            }

            return goSearch(true) ?? goSearch(false);
        }
        #endregion
    }
}
