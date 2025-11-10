using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.Settings;
using System.Net;
using System.Text;

namespace Online.Controllers
{
    public class IptvOnline : BaseOnlineController
    {
        #region Bind
        [HttpGet]
        [AllowAnonymous]
        [Route("lite/iptvonline/bind")]
        public ActionResult Bind(string KEY, string ID)
        {
            string html = string.Empty;

            if (string.IsNullOrWhiteSpace(KEY) || string.IsNullOrWhiteSpace(ID))
            {
                html = "Введите данные <a href='https://iptv.online/ru/dealers/api' target=_blank>https://iptv.online/ru/dealers/api</a> <br> <br><form method=\"get\" action=\"/lite/iptvonline/bind\"><input type=\"text\" name=\"KEY\" placeholder=\"X-API-KEY\"> &nbsp; &nbsp; <input type=\"text\" name=\"ID\" placeholder=\"X-API-ID\"><br><br><button>Авторизоваться</button></form> ";
            }
            else
            {
                html = "Добавьте в init.conf<br><br>\"IptvOnline\": {<br>&nbsp;&nbsp;\"enable\": true,<br>&nbsp;&nbsp;\"token\": \"" + $"{ID}:{KEY}" + "\"<br>}";
            }

            return Content(html, "text/html; charset=utf-8");
        }
        #endregion

        [HttpGet]
        [Route("lite/iptvonline")]
        async public ValueTask<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int serial = -1, int s = -1, bool rjson = false, bool origsource = false)
        {
            var init = await loadKit(AppInit.conf.IptvOnline);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrEmpty(init.token))
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            #region AUTH
            if (!hybridCache.TryGetValue($"iptvonline:auth:{init.token}", out string codeauth))
            {
                var auth = await Http.Post<JObject>($"{init.host}/v1/api/auth", "", timeoutSeconds: 8, proxy: proxy, useDefaultHeaders: false, headers: HeadersModel.Init(
                    ("X-API-KEY", init.token.Split(":")[1]),
                    ("X-API-ID", init.token.Split(":")[0])
                ));

                if (auth == null)
                {
                    proxyManager.Refresh();
                    return null;
                }

                string code = auth.Value<string>("code");
                if (string.IsNullOrEmpty(code))
                    return null;

                codeauth = code;
                hybridCache.Set($"iptvonline:auth:{init.token}", codeauth, DateTime.Now.AddHours(2));
            }
            #endregion

            var data = await search(init, proxyManager, proxy, codeauth, serial, imdb_id, kinopoisk_id, title, original_title);
            if (data == null)
                return OnError();

            if (data.Value<string>("message") != null)
                return ShowError(data.Value<string>("message"));

            #region media
            string id = data.Value<string>("id");
            var cache = await InvokeCache<JToken>($"IptvOnline:{id}:{init.token}", cacheTime(20, init: init), proxyManager, async res =>
            {
                var header = httpHeaders(init, HeadersModel.Init(
                    ("X-API-AUTH", codeauth),
                    ("X-API-ID", init.token.Split(":")[0])
                ));

                string uri = $"{init.host}/v1/api/media/{(serial == 1 ? "serials" : "movies")}/{id}/";
                var root = await Http.Get<JObject>(init.cors(uri), timeoutSeconds: 8, proxy: proxy, headers: header, useDefaultHeaders: false);

                if (root == null || !root.ContainsKey("data"))
                    return res.Fail("data");

                return root["data"];
            });
            #endregion

            return OnResult(cache, () =>
            {
                if (cache.Value.Value<string>("category") == "movie")
                {
                    #region Фильм
                    var mtpl = new MovieTpl(title, original_title, 1);

                    string stream = HostStreamProxy(init, cache.Value["medias"].First.Value<string>("url") + "#.m3u8", proxy: proxy);
                    string quality = cache.Value.Value<int?>("quality")?.ToString();
                    if (quality != null)
                        quality += "p";

                    mtpl.Append(quality ?? title, stream, vast: init.vast);

                    return rjson ? mtpl.ToJson() : mtpl.ToHtml();
                    #endregion
                }
                else
                {
                    #region Сериал
                    if (s == -1)
                    {
                        string enc_title = HttpUtility.UrlEncode(title);
                        string enc_original_title = HttpUtility.UrlEncode(original_title);

                        string quality = cache.Value.Value<string>("quality");
                        if (quality != null)
                            quality += "p";

                        var tpl = new SeasonTpl(quality);

                        foreach (var media in cache.Value["medias"])
                        {
                            int season = media.Value<int>("season");
                            string link = $"{host}/lite/iptvonline?rjson={rjson}&serial={serial}&kinopoisk_id={kinopoisk_id}&imdb_id={imdb_id}&title={enc_title}&original_title={enc_original_title}&s={season}";
                            tpl.Append($"{season} сезон", link, season);
                        }

                        return rjson ? tpl.ToJson() : tpl.ToHtml();
                    }
                    else
                    {
                        var etpl = new EpisodeTpl();
                        string sArhc = s.ToString();

                        foreach (var episode in cache.Value["medias"].First(i => i.Value<int>("season") == s).Value<JArray>("episodes"))
                        {
                            string name = episode.Value<string>("title");
                            string file = episode.Value<string>("url") + "#.m3u8";

                            if (string.IsNullOrEmpty(file))
                                continue;

                            string stream = HostStreamProxy(init, file, proxy: proxy);
                            etpl.Append(name ?? $"{episode.Value<int>("episode")} серия", title ?? original_title, sArhc, episode.Value<int>("episode").ToString(), stream, vast: init.vast);
                        }

                        return rjson ? etpl.ToJson() : etpl.ToHtml();
                    }
                    #endregion
                }

            }, origsource: origsource);
        }


        #region search
        async ValueTask<JToken> search(OnlinesSettings init, ProxyManager proxyManager, WebProxy proxy, string codeauth, int serial, string imdb_id, long kinopoisk_id, string title, string original_title)
        {
            string memKey = $"iptvonline:view:{kinopoisk_id}:{imdb_id}:{title}:{original_title}";

            if (!hybridCache.TryGetValue(memKey, out JToken data))
            {
                string stitle = StringConvert.SearchName(title);
                string sorigtitle = StringConvert.SearchName(original_title);

                async Task<JToken> goSearch(string search)
                {
                    if (string.IsNullOrEmpty(search))
                        return null;

                    var header = httpHeaders(init, HeadersModel.Init(
                        ("X-API-AUTH", codeauth),
                        ("X-API-ID", init.token.Split(":")[0])
                    ));

                    string uri = $"{init.host}/v1/api/media/{(serial == 1 ? "serials" : "movies")}";
                    var data = new System.Net.Http.StringContent(JsonConvert.SerializeObject(new { search }), Encoding.UTF8, "application/json");

                    var video = await Http.Get<JObject>(uri, body: data, timeoutSeconds: 8, proxy: proxy, headers: header, useDefaultHeaders: false);

                    if (video == null)
                    {
                        proxyManager.Refresh();
                        return null;
                    }

                    if (video.ContainsKey("message") && video.Value<string>("message") == "No Subscribed By Media API")
                        return video;

                    if (!video.ContainsKey("data"))
                        return null;

                    foreach (var item in video["data"])
                    {
                        if (kinopoisk_id > 0)
                        {
                            if (item.Value<long?>("kinopoisk") == kinopoisk_id) 
                                return item;   
                        }

                        if (!string.IsNullOrEmpty(imdb_id))
                        {
                            if ($"tt{item.Value<long?>("imdb")}" == imdb_id)
                                return item;
                        }
                    }

                    foreach (var item in video["data"])
                    {
                        if (sorigtitle != null)
                        {
                            if (StringConvert.SearchName(item.Value<string>("orig_title")) == sorigtitle)
                                return item;
                        }

                        if (stitle != null)
                        {
                            if (StringConvert.SearchName(item.Value<string>("ru_title")) == stitle)
                                return item;
                        }
                    }

                    return null;
                }

                data = await goSearch(original_title) ?? await goSearch(title);
                if (data == null)
                    return null;

                proxyManager.Success();
                hybridCache.Set(memKey, data, cacheTime(30, init: init));
            }

            return data;
        }
        #endregion
    }
}
