using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;
using System.Web;
using Lampac.Engine;
using Lampac.Engine.CORE;
using System.Linq;
using Lampac.Models.LITE;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Lampac.Controllers.LITE
{
    public class Rezka : BaseController
    {
        [HttpGet]
        [Route("lite/rezka")]
        async public Task<ActionResult> Index(string title, string original_title, int clarification, string original_language, int year, int s = -1, string href = null)
        {
            if (!AppInit.conf.Rezka.enable)
                return Content(string.Empty);

            if (original_language != "en")
                clarification = 1;

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            #region embed
            var result = await embed(title, original_title, clarification, year, href);
            if (result.content == null)
            {
                if (string.IsNullOrWhiteSpace(href) && result.similar != null && result.similar.Count > 0)
                {
                    foreach (var similar in result.similar)
                    {
                        string link = $"{host}/lite/rezka?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&clarification={clarification}&year={year}&href={HttpUtility.UrlEncode(similar.href)}";

                        html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\",\"similar\":true}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + similar.title + "</div></div></div>";
                        firstjson = false;
                    }

                    return Content(html + "</div>", "text/html; charset=utf-8");
                }

                return Content(string.Empty);
            }
            #endregion

            if (!result.content.Contains("data-season_id="))
            {
                #region Фильм
                var match = new Regex("<li [^>]+ data-translator_id=\"([0-9]+)\" ([^>]+)>([^<]+)").Match(result.content);
                if (match.Success)
                {
                    while (match.Success)
                    {
                        if (!string.IsNullOrEmpty(match.Groups[1].Value) && !string.IsNullOrEmpty(match.Groups[3].Value))
                        {
                            string link = $"{host}/lite/rezka/episode?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&id={result.id}&t={match.Groups[1].Value}";
                            string voice = match.Groups[3].Value.Trim();
                            if (voice == "-")
                                voice = "Оригинал";

                            if (match.Groups[2].Value.Contains("data-director=\"1\""))
                                link += "&director=1";

                            html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"call\",\"url\":\"" + link + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + voice + "</div></div>";
                            firstjson = false;
                        }

                        match = match.NextMatch();
                    }
                }
                else
                {
                    string json = new Regex("\"id\":\"cdnplayer\",\"streams\":\"([^\"]+)\"").Match(result.content).Groups[1].Value;
                    var links = getStreamLink(json.Replace("\\", ""), isfilm: true);

                    string streansquality = string.Empty;
                    foreach (var l in links)
                        streansquality += $"\"{l.title}\":\"" + l.stream_url + "\",";

                    html += "<div class=\"videos__item videos__movie selector focused\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + links[0].stream_url + "\",\"title\":\"" + title + "\", \"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + links[0].title + "</div></div>";
                }
                #endregion
            }
            else
            {
                #region Сериал
                string trs = new Regex("\\.initCDNSeriesEvents\\([0-9]+, ([0-9]+),").Match(result.content).Groups[1].Value;
                if (string.IsNullOrWhiteSpace(trs))
                    return Content(string.Empty);

                #region Перевод
                if (result.content.Contains("data-translator_id="))
                {
                    var match = new Regex("data-translator_id=\"([0-9]+)\">([^<]+)(<img title=\"([^\"]+)\" [^>]+/>)?").Match(result.content);
                    while (match.Success)
                    {
                        string name = match.Groups[2].Value.Trim() + (string.IsNullOrWhiteSpace(match.Groups[4].Value) ? "" : $" ({match.Groups[4].Value})");
                        string link = $"{host}/lite/rezka/serial?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&clarification={clarification}&year={year}&href={HttpUtility.UrlEncode(href)}&id={result.id}&t={match.Groups[1].Value}";

                        html += "<div class=\"videos__button selector " + (match.Groups[1].Value == trs ? "active" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + name + "</div>";

                        match = match.NextMatch();
                    }

                    html += "</div><div class=\"videos__line\">";
                }
                #endregion

                var m = Regex.Match(result.content, "data-cdn_url=\"([^\"]+)\" [^>]+ data-season_id=\"([0-9]+)\" data-episode_id=\"([0-9]+)\">([^>]+)</li>");
                while (m.Success)
                {
                    if (s == -1)
                    {
                        #region Сезоны
                        if (!string.IsNullOrEmpty(m.Groups[2].Value) && !html.Contains($"{m.Groups[2].Value} сезон"))
                        {
                            string link = $"{host}/lite/rezka?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&clarification={clarification}&year={year}&href={HttpUtility.UrlEncode(href)}&t={trs}&s={m.Groups[2].Value}";

                            html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{m.Groups[2].Value} сезон" + "</div></div></div>";
                            firstjson = false;
                        }
                        #endregion
                    }
                    else
                    {
                        #region Серии
                        if (m.Groups[2].Value == s.ToString() && !html.Contains(m.Groups[4].Value))
                        {
                            string link = $"{host}/lite/rezka/episode?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&id={result.id}&t={trs}&s={s}&e={m.Groups[3].Value}";

                            html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + m.Groups[3].Value + "\" data-json='{\"method\":\"call\",\"url\":\"" + link + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + m.Groups[4].Value + "</div></div>";
                            firstjson = false;
                        }
                        #endregion
                    }

                    m = m.NextMatch();
                }
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }


        #region Serial
        [HttpGet]
        [Route("lite/rezka/serial")]
        async public Task<ActionResult> Serial(string title, string original_title, int clarification, int year, string href, long id, int t, int s = -1)
        {
            if (!AppInit.conf.Rezka.enable)
                return Content(string.Empty);

            #region Кеш запроса
            string memKey = $"rezka:view:serial:{id}:{t}";

            if (!memoryCache.TryGetValue(memKey, out JObject root))
            {
                string uri = $"{AppInit.conf.Rezka.host}/ajax/get_cdn_series/?t={((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds()}";
                string data = $"id={id}&translator_id={t}&action=get_episodes";

                root = await HttpClient.Post<JObject>(uri, data, timeoutSeconds: 10, addHeaders: new List<(string name, string val)>
                {
                    ("X-App-Hdrezka-App", "1"),
                    ("Cookie", AppInit.conf.Rezka.сookie)
                });

                if (root == null || !root.ContainsKey("episodes"))
                    return Content(string.Empty);

                string episodes = root.Value<object>("episodes")?.ToString();
                if (string.IsNullOrWhiteSpace(episodes) || episodes.ToLower() == "false")
                    return Content(string.Empty);

                memoryCache.Set(memKey, root, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 10));
            }
            #endregion

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            #region Перевод
            {
                var result = await embed(title, original_title, clarification, year, href);
                if (result.content != null)
                {
                    if (result.content.Contains("data-translator_id="))
                    {
                        var match = new Regex("data-translator_id=\"([0-9]+)\">([^<]+)(<img title=\"([^\"]+)\" [^>]+/>)?").Match(result.content);
                        while (match.Success)
                        {
                            string name = match.Groups[2].Value.Trim() + (string.IsNullOrWhiteSpace(match.Groups[4].Value) ? "" : $" ({match.Groups[4].Value})");
                            string link = $"{host}/lite/rezka/serial?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&clarification={clarification}&year={year}&href={HttpUtility.UrlEncode(href)}&id={id}&t={match.Groups[1].Value}";

                            html += "<div class=\"videos__button selector " + (match.Groups[1].Value == t.ToString() ? "active" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + name + "</div>";

                            match = match.NextMatch();
                        }

                        html += "</div><div class=\"videos__line\">";
                    }
                }
            }
            #endregion

            if (s == -1)
            {
                #region Сезоны
                var match = new Regex("data-tab_id=\"([0-9]+)\">([^<]+)</li>").Match(root.Value<string>("seasons"));
                while (match.Success)
                {
                    string link = $"{host}/lite/rezka/serial?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&clarification={clarification}&year={year}&href={HttpUtility.UrlEncode(href)}&id={id}&t={t}&s={match.Groups[1].Value}";

                    html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{match.Groups[1].Value} сезон" + "</div></div></div>";
                    firstjson = false;

                    match = match.NextMatch();
                }
                #endregion
            }
            else
            {
                #region Серии
                var m = new Regex($"data-season_id=\"{s}\" data-episode_id=\"([0-9]+)\">([^<]+)</li>").Match(root.Value<string>("episodes"));
                while (m.Success)
                {
                    if (!string.IsNullOrEmpty(m.Groups[1].Value) && !string.IsNullOrEmpty(m.Groups[2].Value))
                    {
                        string link = $"{host}/lite/rezka/episode?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&id={id}&t={t}&s={s}&e={m.Groups[1].Value}";

                        html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + m.Groups[1].Value + "\" data-json='{\"method\":\"call\",\"url\":\"" + link + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + m.Groups[2].Value + "</div></div>";
                        firstjson = false;
                    }

                    m = m.NextMatch();
                }
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }
        #endregion

        #region Episode
        [HttpGet]
        [Route("lite/rezka/episode")]
        async public Task<ActionResult> Movie(string title, string original_title, long id, int t, int director = 0, int s = -1, int e = -1)
        {
            if (!AppInit.conf.Rezka.enable)
                return Content(string.Empty);

            #region Кеш запроса
            string memKey = $"rezka:view:get_cdn_series:{id}:{t}:{director}:{s}:{e}";

            if (!memoryCache.TryGetValue(memKey, out JObject root))
            {
                string uri = $"{AppInit.conf.Rezka.host}/ajax/get_cdn_series/?t={((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds()}"; 
                string data = null;

                if (s == -1)
                {
                    data = $"id={id}&translator_id={t}&is_camrip=0&is_ads=0&is_director={director}&action=get_movie";
                }
                else
                {
                    data = $"id={id}&translator_id={t}&season={s}&episode={e}&action=get_stream";
                }

                root = await HttpClient.Post<JObject>(uri, data, timeoutSeconds: 10, addHeaders: new List<(string name, string val)> 
                { 
                    ("X-App-Hdrezka-App", "1"),
                    ("Cookie", AppInit.conf.Rezka.сookie)
                });
                
                if (root == null || !root.ContainsKey("url"))
                    return Content(string.Empty);

                string url = root.Value<object>("url")?.ToString();
                if (string.IsNullOrWhiteSpace(url) || url.ToLower() == "false")
                    return Content(string.Empty);

                memoryCache.Set(memKey, root, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 10));
            }
            #endregion

            #region subtitle
            string subtitles = string.Empty;

            string subtitlehtml = root.Value<object>("subtitle")?.ToString();
            if (!string.IsNullOrWhiteSpace(subtitlehtml))
            {
                var m = Regex.Match(subtitlehtml, "\\[([^\\]]+)\\](https?://[^\n\r,']+\\.vtt)");
                while (m.Success)
                {
                    if (!string.IsNullOrEmpty(m.Groups[1].Value) && !string.IsNullOrEmpty(m.Groups[2].Value))
                        subtitles += "{\"label\": \"" + m.Groups[1].Value + "\",\"url\": \"" + HostStreamProxy(AppInit.conf.Rezka.streamproxy, m.Groups[2].Value) + "\"},";

                    m = m.NextMatch();
                }
            }
            #endregion

            var links = getStreamLink(root.Value<string>("url"), isfilm: true);

            string streansquality = string.Empty;
            foreach (var l in links)
                streansquality += $"\"{l.title}\":\"" + l.stream_url + "\",";

            return Content("{\"method\":\"play\",\"url\":\"" + links[0].stream_url + "\",\"title\":\"" + (title ?? original_title) + "\", \"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}, \"subtitles\": [" + Regex.Replace(subtitles, ",$", "") + "]}", "application/json; charset=utf-8");
        }
        #endregion


        #region decodeBase64
        static string decodeBase64(string _data)
        {
            if (_data.Contains("#"))
            {
                string[] trashList = new string[] { "QEA=", "QCM=", "QCE=", "QF4=", "QCQ=", "I0A=", "IyM=", "IyE=", "I14=", "IyQ=", "IUA=", "ISM=", "ISE=", "IV4=", "ISQ=", "XkA=", "XiM=", "XiE=", "Xl4=", "XiQ=", "JEA=", "JCM=", "JCE=", "JF4=", "JCQ=", "QEBA", "QEAj", "QEAh", "QEBe", "QEAk", "QCNA", "QCMj", "QCMh", "QCNe", "QCMk", "QCFA", "QCEj", "QCEh", "QCFe", "QCEk", "QF5A", "QF4j", "QF4h", "QF5e", "QF4k", "QCRA", "QCQj", "QCQh", "QCRe", "QCQk", "I0BA", "I0Aj", "I0Ah", "I0Be", "I0Ak", "IyNA", "IyMj", "IyMh", "IyNe", "IyMk", "IyFA", "IyEj", "IyEh", "IyFe", "IyEk", "I15A", "I14j", "I14h", "I15e", "I14k", "IyRA", "IyQj", "IyQh", "IyRe", "IyQk", "IUBA", "IUAj", "IUAh", "IUBe", "IUAk", "ISNA", "ISMj", "ISMh", "ISNe", "ISMk", "ISFA", "ISEj", "ISEh", "ISFe", "ISEk", "IV5A", "IV4j", "IV4h", "IV5e", "IV4k", "ISRA", "ISQj", "ISQh", "ISRe", "ISQk", "XkBA", "XkAj", "XkAh", "XkBe", "XkAk", "XiNA", "XiMj", "XiMh", "XiNe", "XiMk", "XiFA", "XiEj", "XiEh", "XiFe", "XiEk", "Xl5A", "Xl4j", "Xl4h", "Xl5e", "Xl4k", "XiRA", "XiQj", "XiQh", "XiRe", "XiQk", "JEBA", "JEAj", "JEAh", "JEBe", "JEAk", "JCNA", "JCMj", "JCMh", "JCNe", "JCMk", "JCFA", "JCEj", "JCEh", "JCFe", "JCEk", "JF5A", "JF4j", "JF4h", "JF5e", "JF4k", "JCRA", "JCQj", "JCQh", "JCRe", "JCQk" };

                _data = _data.Remove(0, 2).Replace("//_//", "");

                foreach (string trash in trashList)
                    _data = _data.Replace(trash, "");

                _data = Regex.Replace(_data, "//[^/]+_//", "").Replace("//_//", "");
                _data = Encoding.UTF8.GetString(Convert.FromBase64String(_data));


                //_data = Regex.Replace(_data, "/[^/=]+=([^\n\r\t= ])", "/$1");
                //_data = _data.Replace("//_//", "");
                //_data = Regex.Replace(_data, "//[^/]+_//", "");
                //_data = _data.Replace("QEBAQEAhIyMhXl5e", "");
                //_data = _data.Replace("IyMjI14hISMjIUBA", "");
                //_data = Regex.Replace(_data, "CQhIUAkJEBeIUAjJCRA.", "");
                //_data = _data.Replace("QCMhQEBAIyMkJXl5eXl5eIyNAEBA", "");

                //// Lampa
                //_data = _data.Replace("QCMhQEBAIyMkJEBA", "");
                //_data = _data.Replace("Xl5eXl5eIyNAzN2FkZmRm", "");

                //_data = Encoding.UTF8.GetString(Convert.FromBase64String(_data.Remove(0, 2)));
            }

            return _data;
        }
        #endregion

        #region getStreamLink
        List<ApiModel> getStreamLink(string _data, bool isfilm = false)
        {
            _data = decodeBase64(_data);
            var links = new List<ApiModel>();

            #region getLink
            string getLink(string _q)
            {
                string link = new Regex($"\\[{_q}\\][^ ]+ or (https?://[^\n\r ]+.mp4)(,|$)").Match(_data).Groups[1].Value;

                if (string.IsNullOrWhiteSpace(link))
                    link = new Regex($"\\[{_q}\\](https?://[^\n\r, ]+.mp4([^\n\r, ]+)?)").Match(_data).Groups[1].Value;

                if (string.IsNullOrWhiteSpace(link) || !Regex.IsMatch(link, "^[a-z0-9/\\-:\\.\\=]+$", RegexOptions.IgnoreCase))
                    return null;

                return link;
            }
            #endregion

            #region Максимально доступное
            foreach (var q in new List<string> { "2160p", "1440p", "1080p Ultra", "1080p", "720p", "480p", "360p", "240p" })
            {
                string link = getLink(q);
                if (string.IsNullOrEmpty(link))
                    continue;

                links.Add(new ApiModel()
                {
                    title = q.Contains("p") ? q : $"{q}p",
                    stream_url = HostStreamProxy(AppInit.conf.Rezka.streamproxy, link)
                });
            }
            #endregion

            return links;
        }
        #endregion


        #region embed
        async ValueTask<(string content, string id, List<(string title, string href)> similar)> embed(string title, string original_title, int clarification, int year, string href)
        {
            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(title) || year == 0))
                return (null, null, null);

            string memKey = $"rezka:view:{title}:{original_title}:{year}:{clarification}:{href}";

            if (!memoryCache.TryGetValue(memKey, out (string content, string id, List<(string title, string href)> similar) result))
            {
                System.Net.WebProxy proxy = null;
                if (AppInit.conf.Rezka.useproxy)
                    proxy = HttpClient.webProxy();

                string link = href, reservedlink = null;

                if (string.IsNullOrWhiteSpace(link))
                {
                    string search = await HttpClient.Get($"{AppInit.conf.Rezka.host}/search/?do=search&subaction=search&q={HttpUtility.UrlEncode(clarification == 1 ? title : (original_title ?? title))}", timeoutSeconds: 8, proxy: proxy);
                    if (search == null)
                        return (null, null, null);

                    foreach (string row in search.Split("\"b-content__inline_item\"").Skip(1))
                    {
                        var g = Regex.Match(row, "href=\"(https?://[^\"]+)\">([^<]+)</a> ?<div>([0-9]{4})").Groups;

                        if (string.IsNullOrWhiteSpace(g[1].Value))
                            continue;

                        string name = g[2].Value.ToLower().Trim();
                        if (result.similar == null)
                            result.similar = new List<(string title, string href)>();

                        result.similar.Add(($"{name} {g[3].Value}", g[1].Value));

                        if ((name.Contains(" / ") && name.Contains(title.ToLower())) || name == title.ToLower())
                        {
                            if (g[3].Value == year.ToString())
                            {
                                reservedlink = g[1].Value;
                                link = reservedlink;
                                break;
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(link))
                    {
                        if (string.IsNullOrWhiteSpace(reservedlink))
                            return (null, null, result.similar);

                        link = reservedlink;
                    }
                }

                result.id = Regex.Match(link, "/([0-9]+)-[^/]+\\.html").Groups[1].Value;
                result.content = await HttpClient.Get(link, timeoutSeconds: 8, proxy: proxy);
                if (result.content == null || string.IsNullOrWhiteSpace(result.id))
                    return (null, null, null);

                memoryCache.Set(memKey, result, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 10));
            }

            return result;
        }
        #endregion
    }
}
