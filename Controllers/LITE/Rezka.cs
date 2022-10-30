using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Text;
using System.Web;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.LITE;

namespace Lampac.Controllers.LITE
{
    public class Rezka : BaseController
    {
        [HttpGet]
        [Route("lite/rezka")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, string t)
        {
            if (!AppInit.conf.Rezka.enable)
                return Content(string.Empty);

            if (kinopoisk_id == 0 && string.IsNullOrWhiteSpace(imdb_id))
                return Content(string.Empty);

            string content = await embed(memoryCache, imdb_id, kinopoisk_id, t);
            if (content == null)
                return Content(string.Empty);

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (!content.Contains("id=\"season-number\""))
            {
                #region Фильм
                var m = Regex.Match(content, "<option data-token=\"([^\"]+)\" [^>]+>([^<]+)</option>");
                while (m.Success)
                {
                    if (!string.IsNullOrEmpty(m.Groups[1].Value) && !string.IsNullOrEmpty(m.Groups[2].Value))
                    {
                        string link = $"{AppInit.Host(HttpContext)}/lite/rezka/movie?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&t={m.Groups[1].Value}";
                        string voice = m.Groups[2].Value.Trim();
                        if (voice == "-")
                            voice = "Оригинал";

                        html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"call\",\"url\":\"" + link + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + voice + "</div></div>";
                        firstjson = false;
                    }

                    m = m.NextMatch();
                }
                #endregion
            }
            else
            {
                #region Перевод
                string activTranslate = t;

                var m = Regex.Match(content, "<option data-token=\"([^\"]+)\" [^>]+>([^<]+)</option>");
                while (m.Success)
                {
                    if (!string.IsNullOrEmpty(m.Groups[1].Value) && !string.IsNullOrEmpty(m.Groups[2].Value))
                    {
                        if (string.IsNullOrWhiteSpace(activTranslate))
                            activTranslate = m.Groups[1].Value;

                        string link = $"{AppInit.Host(HttpContext)}/lite/rezka?imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&t={m.Groups[1].Value}";

                        string active = string.IsNullOrWhiteSpace(t) ? (firstjson ? "active" : "") : (t == m.Groups[1].Value ? "active" : "");

                        string voice = m.Groups[2].Value.Trim();
                        if (voice != "-")
                        {
                            html += "<div class=\"videos__button selector " + active + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + voice + "</div>";
                            firstjson = false;
                        }
                    }

                    m = m.NextMatch();
                }

                html += "</div>";
                #endregion

                #region Сезоны
                firstjson = true;
                html += "<div class=\"videos__line\">";

                m = Regex.Match(StringConvert.FindLastText(content, "name=\"season\"", "</select>"), "<option value=\"([0-9]+)\"([^>]+)?>([^<]+)</option>");
                while (m.Success)
                {
                    if (!string.IsNullOrEmpty(m.Groups[1].Value) && !string.IsNullOrEmpty(m.Groups[3].Value))
                    {
                        string link = $"{AppInit.Host(HttpContext)}/lite/rezka/serial?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&t={activTranslate}&s={m.Groups[1].Value}";

                        html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + m.Groups[3].Value + "</div></div></div>";
                        firstjson = false;
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
        async public Task<ActionResult> Serial(string title, string original_title, string t, int s)
        {
            if (!AppInit.conf.Rezka.enable)
                return Content(string.Empty);

            #region Кеш запроса
            string memKey = $"rezka:view:serial:{t}:{s}";

            if (!memoryCache.TryGetValue(memKey, out string content))
            {
                content = await HttpClient.Get($"{AppInit.conf.Rezka.host}/serial/{t}/iframe?s={s}", timeoutSeconds: 8, useproxy: AppInit.conf.Rezka.useproxy, MaxResponseContentBufferSize: 20_000_000);
                if (content == null)
                    return Content(string.Empty);

                memoryCache.Set(memKey, content, DateTime.Now.AddMinutes(10));
            }
            #endregion

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            var m = Regex.Match(StringConvert.FindLastText(content, "name=\"episode\"", "</select>"), "<option value=\"([^\"]+)\"([^>]+)?>([^<]+)</option>");
            while (m.Success)
            {
                if (!string.IsNullOrEmpty(m.Groups[1].Value) && !string.IsNullOrEmpty(m.Groups[3].Value))
                {
                    string link = $"{AppInit.Host(HttpContext)}/lite/rezka/episode?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&t={t}&s={s}&e={m.Groups[1].Value}";

                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + m.Groups[1].Value + "\" data-json='{\"method\":\"call\",\"url\":\"" + link + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + m.Groups[3].Value + "</div></div>";
                    firstjson = false;
                }

                m = m.NextMatch();
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }
        #endregion

        #region Movie / Episode
        [HttpGet]
        [Route("lite/rezka/movie")]
        [Route("lite/rezka/episode")]
        async public Task<ActionResult> Movie(string title, string original_title, string t, int s, int e)
        {
            if (!AppInit.conf.Rezka.enable)
                return Content(string.Empty);

            #region Кеш запроса
            string memKey = $"rezka:view:stream:{t}:{s}:{e}";

            if (!memoryCache.TryGetValue(memKey, out string content))
            {
                string uri = $"{AppInit.conf.Rezka.host}/movie/{t}/iframe";
                if (s > 0 || e > 0)
                    uri = $"{AppInit.conf.Rezka.host}/serial/{t}/iframe?s={s}&e={e}";

                content = await HttpClient.Get(uri, timeoutSeconds: 8, useproxy: AppInit.conf.Rezka.useproxy, MaxResponseContentBufferSize: 20_000_000);
                if (content == null)
                    return Content(string.Empty);

                memoryCache.Set(memKey, content, DateTime.Now.AddMinutes(10));
            }
            #endregion

            #region subtitle
            string subtitles = string.Empty;

            string subtitlehtml = Regex.Match(content, "'subtitle': '([^']+)'").Groups[1].Value;
            if (!string.IsNullOrWhiteSpace(subtitlehtml))
            {
                var m = Regex.Match(subtitlehtml, "\\[([^\\]]+)\\](https?://[^\n\r,']+\\.vtt)");
                while (m.Success)
                {
                    if (!string.IsNullOrEmpty(m.Groups[1].Value) && !string.IsNullOrEmpty(m.Groups[2].Value))
                    {
                        string suburl = m.Groups[2].Value.Replace("https:", "http:");
                        subtitles += "{\"label\": \"" + m.Groups[1].Value + "\",\"url\": \"" + (AppInit.conf.Rezka.streamproxy ? $"{AppInit.Host(HttpContext)}/proxy/{suburl}" : suburl) + "\"},";
                    }

                    m = m.NextMatch();
                }
            }
            #endregion

            var links = getStreamLink(Regex.Match(content, "'file': ?'([^']+)'").Groups[1].Value.Trim(), isfilm: true);

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

                return link.Replace("https:", "http:");
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
                    stream_url = AppInit.conf.Rezka.streamproxy ? $"{AppInit.Host(HttpContext)}/proxy/{link}" : link
                });
            }
            #endregion

            return links;
        }
        #endregion



        #region embed
        async static ValueTask<string> embed(IMemoryCache memoryCache, string imdb_id, long kinopoisk_id, string t)
        {
            string memKey = $"rezka:view:{kinopoisk_id}:{imdb_id}:{t}";

            if (!memoryCache.TryGetValue(memKey, out string content))
            {
                string uri = $"{AppInit.conf.Rezka.host}/embed/" + (kinopoisk_id > 0 ? kinopoisk_id.ToString() : imdb_id);
                if (!string.IsNullOrWhiteSpace(t))
                    uri = $"{AppInit.conf.Rezka.host}/serial/{t}/iframe";

                // Получаем html
                content = await HttpClient.Get(uri, timeoutSeconds: 8, useproxy: AppInit.conf.Rezka.useproxy, MaxResponseContentBufferSize: 20_000_000);
                if (content == null)
                    return null;

                memoryCache.Set(memKey, content, DateTime.Now.AddMinutes(10));
            }

            return content;
        }
        #endregion
    }
}
