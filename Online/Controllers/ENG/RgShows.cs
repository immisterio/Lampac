using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;
using Lampac.Models.LITE;
using Newtonsoft.Json.Linq;
using Lampac.Engine.CORE;
using System.Web;
using System.Net;

namespace Lampac.Controllers.LITE
{
    public class RgShows : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/rgshows")]
        async public Task<ActionResult> Index(bool checksearch, long id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.Rgshows);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrEmpty(imdb_id))
                return OnError();

            if (serial == 1)
            {
                #region Сериал
                var tmdb = await InvokeCache<JToken>($"tmdb:seasons:{id}", cacheTime(40, init: init), async res =>
                {
                    var root = await HttpClient.Get<JObject>($"{AppInit.conf.cub.scheme}://tmdb.{AppInit.conf.cub.mirror}/3/tv/{id}?api_key={AppInit.conf.tmdb.api_key}");

                    if (root == null || !root.ContainsKey("seasons"))
                        return res.Fail("seasons");

                    return root["seasons"];
                });

                if (!tmdb.IsSuccess)
                    return OnError(tmdb.ErrorMsg);

                if (s == -1)
                {
                    #region Сезоны
                    var tpl = new SeasonTpl();

                    foreach (var season in tmdb.Value)
                    {
                        int number = season.Value<int>("season_number");
                        if (1 > number)
                            continue;

                        string link = $"{host}/lite/rgshows?id={id}&imdb_id={imdb_id}&serial=1&rjson={rjson}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={number}";
                        tpl.Append($"{number} сезон", link, number);
                    }

                    return ContentTo(rjson ? tpl.ToJson() : tpl.ToHtml());
                    #endregion
                }
                else
                {
                    #region Серии
                    var etpl = new EpisodeTpl();

                    foreach (var season in tmdb.Value)
                    {
                        if (season.Value<int>("season_number") != s)
                            continue;

                        for (int i = 1; i <= season.Value<int>("episode_count"); i++)
                            etpl.Append($"{i} серия", title ?? original_title, s.ToString(), i.ToString(), accsArgs($"{host}/lite/rgshows/video.m3u8?imdb_id={imdb_id}&s={s}&e={i}"), vast: init.vast);
                    }

                    return ContentTo(rjson ? etpl.ToJson() : etpl.ToHtml());
                    #endregion
                }
                #endregion
            }
            else
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title);

                mtpl.Append("1080p", accsArgs($"{host}/lite/rgshows/video.m3u8?imdb_id={imdb_id}"), vast: init.vast);

                return ContentTo(rjson ? mtpl.ToJson() : mtpl.ToHtml());
                #endregion
            }
        }


        #region Video
        [HttpGet]
        [Route("lite/rgshows/video.m3u8")]
        async public Task<ActionResult> Video(string imdb_id, int s = -1, int e = -1)
        {
            var init = await loadKit(AppInit.conf.Rgshows);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrEmpty(imdb_id))
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string embed = $"{init.host}/main/movie/{imdb_id}";
            if (s > 0)
                embed = $"{init.host}/main/tv/{imdb_id}/{s}/{e}";

            string m3u = await magic(embed, init, proxy.proxy);
            if (m3u == null)
                return StatusCode(502);

            return Redirect(HostStreamProxy(init, m3u, proxy: proxy.proxy));
        }
        #endregion


        #region magic
        async ValueTask<string> magic(string uri, OnlinesSettings init, WebProxy proxy)
        {
            if (string.IsNullOrEmpty(uri))
                return uri;

            try
            {
                string memKey = $"rgshows:{uri}";
                if (!hybridCache.TryGetValue(memKey, out string m3u8))
                {
                    var root = await HttpClient.Get<JObject>(uri, timeoutSeconds: 30, headers: httpHeaders(init));
                    if (root == null || !root.ContainsKey("stream"))
                        return null;

                    m3u8 = root["stream"].Value<string>("url");
                    if (string.IsNullOrEmpty(m3u8))
                        return null;

                    hybridCache.Set(memKey, m3u8, cacheTime(20, init: init));
                }

                return m3u8;
            }
            catch { return null; }
        }
        #endregion
    }
}
