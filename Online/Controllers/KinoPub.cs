using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Web;
using Lampac.Engine.CORE;
using Lampac.Models.LITE.KinoPub;
using Newtonsoft.Json.Linq;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;
using Online;
using Shared.Engine.CORE;

namespace Lampac.Controllers.LITE
{
    public class KinoPub : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager("kinopub", AppInit.conf.KinoPub);

        #region kinopubpro
        [HttpGet]
        [Route("lite/kinopubpro")]
        async public Task<ActionResult> Pro(string code)
        {
            var proxy = proxyManager.Get();

            if (string.IsNullOrWhiteSpace(code))
            {
                var token_request = await HttpClient.Post<JObject>($"{AppInit.conf.KinoPub.apihost}/oauth2/device?grant_type=device_code&client_id=xbmc&client_secret=cgg3gtifu46urtfp2zp1nqtba0k2ezxh", "", proxy: proxy);

                string html = "1. Откройте <a href='https://kino.pub/device'>https://kino.pub/device</a> <br>";
                html += $"2. Введите код активации <b>{token_request.Value<string>("user_code")}</b><br>";
                html += $"3. Когда на сайте kino.pub появится \"Ожидание устройства\", нажмите кнопку \"Проверить активацию\" которая ниже</b>";

                html += $"<br><br><a href='/lite/kinopubpro?code={token_request.Value<string>("code")}'><button>Проверить активацию</button></a>";

                return Content(html, "text/html; charset=utf-8");
            }
            else
            {
                var device_token = await HttpClient.Post<JObject>($"{AppInit.conf.KinoPub.apihost}/oauth2/device?grant_type=device_token&client_id=xbmc&client_secret=cgg3gtifu46urtfp2zp1nqtba0k2ezxh&code={code}", "", proxy: proxy);
                if (device_token == null || string.IsNullOrWhiteSpace(device_token.Value<string>("access_token")))
                    return LocalRedirect("/lite/kinopubpro");

                await HttpClient.Post($"{AppInit.conf.KinoPub.apihost}/v1/device/notify?access_token={device_token.Value<string>("access_token")}", "&title=LAMPAC", proxy: proxy);

                return Content($"В init.conf укажите token <b>{device_token.Value<string>("access_token")}</b>", "text/html; charset=utf-8");
            }
        }
        #endregion

        [HttpGet]
        [Route("lite/kinopub")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int clarification, string original_language, int postid, int s = -1)
        {
            if (string.IsNullOrWhiteSpace(AppInit.conf.KinoPub.token))
                return OnError();

            if (original_language != "en")
                clarification = 1;

            postid = postid == 0 ? await search(clarification == 1 ? title : (original_title ?? title), imdb_id, kinopoisk_id) : postid;
            if (postid == 0)
                return OnError();

            var proxy = proxyManager.Get();

            string memKey = $"kinopub:{postid}";
            if (!memoryCache.TryGetValue(memKey, out RootObject root))
            {
                root = await HttpClient.Get<RootObject>($"{AppInit.conf.KinoPub.apihost}/v1/items/{postid}?access_token={AppInit.conf.KinoPub.token}", timeoutSeconds: 8, proxy: proxy);
                if (root?.item?.seasons == null && root?.item?.videos == null)
                    return OnError(proxyManager);

                memoryCache.Set(memKey, root, DateTime.Now.AddMinutes(10));
            }

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (root?.item?.videos != null)
            {
                #region Фильм
                foreach (var v in root.item.videos)
                {
                    #region voicename
                    string voicename = string.Empty;

                    if (v.audios != null)
                    {
                        foreach (var audio in v.audios)
                        {
                            if (audio.lang == "eng")
                            {
                                if (!voicename.Contains(audio.lang))
                                    voicename += "eng, ";
                            }
                            else
                            {
                                string a = audio?.author?.title ?? audio?.type?.title;
                                if (a != null)
                                {
                                    a = $"{a} ({audio.lang})";
                                    if (!voicename.Contains(a))
                                        voicename += $"{a}, ";
                                }
                            }
                        }

                        voicename = Regex.Replace(voicename, "[, ]+$", "");
                    }
                    #endregion

                    if (AppInit.conf.KinoPub.filetype == "hls4")
                    {
                        string hls = HostStreamProxy(AppInit.conf.KinoPub, v.files[0].url.hls4, proxy: proxy);
                        html += "<div class=\"videos__item videos__movie selector focused\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + hls + "\",\"title\":\"" + (title ?? original_title) + "\", \"voice_name\":\"" + voicename + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + v.files[0].quality + "</div></div>";
                    }
                    else
                    {
                        #region subtitle
                        string subtitles = string.Empty;

                        if (v.subtitles != null)
                        {
                            foreach (var sub in v.subtitles)
                            {
                                string suburl = HostStreamProxy(AppInit.conf.KinoPub, sub.url, proxy: proxy);
                                subtitles += "{\"label\": \"" + sub.lang + "\",\"url\": \"" + suburl + "\"},";
                            }

                            subtitles = Regex.Replace(subtitles, ",$", "");
                        }
                        #endregion

                        #region streansquality
                        string streansquality = string.Empty;

                        foreach (var f in v.files)
                        {
                            string l = HostStreamProxy(AppInit.conf.KinoPub, f.url.http, proxy: proxy);
                            streansquality += $"\"{f.quality}\":\"" + l + "\",";
                        }

                        streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";
                        #endregion

                        string mp4 = HostStreamProxy(AppInit.conf.KinoPub, v.files[0].url.http, proxy: proxy);
                        html += "<div class=\"videos__item videos__movie selector focused\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + mp4 + "\",\"title\":\"" + (title ?? original_title) + "\", \"subtitles\": [" + subtitles + "], \"voice_name\":\"" + voicename + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + v.files[0].quality + "</div></div>";
                    }
                }
                #endregion
            }
            else
            {
                #region Сериал
                firstjson = true;

                if (s == -1)
                {
                    #region Сезоны
                    foreach (var season in root.item.seasons)
                    {
                        string link = $"{host}/lite/kinopub?postid={postid}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={season.number}";

                        html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{season.number} сезон" + "</div></div></div>";
                        firstjson = false;
                    }
                    #endregion
                }
                else
                {
                    #region Серии
                    foreach (var episode in root.item.seasons.First(i => i.number == s).episodes)
                    {
                        #region voicename
                        string voicename = string.Empty;

                        if (episode.audios != null)
                        {
                            foreach (var audio in episode.audios)
                            {
                                string a = audio.author?.title ?? audio.lang;
                                if (a != null && !voicename.Contains(a) && a != "rus")
                                    voicename += $"{a}, ";
                            }

                            voicename = Regex.Replace(voicename, "[, ]+$", "");
                        }
                        #endregion

                        if (AppInit.conf.KinoPub.filetype == "hls4")
                        {
                            string hls = HostStreamProxy(AppInit.conf.KinoPub, episode.files[0].url.hls4, proxy: proxy);

                            html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + episode.number + "\" data-json='{\"method\":\"play\",\"url\":\"" + hls + "\",\"title\":\"" + $"{title ?? original_title} ({episode.number} серия)" + "\", \"voice_name\":\"" + voicename + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode.number} серия" + "</div></div>";
                            firstjson = false;
                        }
                        else
                        {
                            #region subtitle
                            string subtitles = string.Empty;

                            if (episode.subtitles != null)
                            {
                                foreach (var sub in episode.subtitles)
                                {
                                    string suburl = HostStreamProxy(AppInit.conf.KinoPub, sub.url, proxy: proxy);
                                    subtitles += "{\"label\": \"" + sub.lang + "\",\"url\": \"" + suburl + "\"},";
                                }

                                subtitles = Regex.Replace(subtitles, ",$", "");
                            }
                            #endregion

                            #region streansquality
                            string streansquality = string.Empty;

                            foreach (var f in episode.files)
                            {
                                string l = HostStreamProxy(AppInit.conf.KinoPub, f.url.http, proxy: proxy);
                                streansquality += $"\"{f.quality}\":\"" + l + "\",";
                            }

                            streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";
                            #endregion

                            string mp4 = HostStreamProxy(AppInit.conf.KinoPub, episode.files[0].url.http, proxy: proxy);

                            html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + episode.number + "\" data-json='{\"method\":\"play\",\"url\":\"" + mp4 + "\",\"title\":\"" + $"{title ?? original_title} ({episode.number} серия)" + "\", \"subtitles\": [" + subtitles + "], \"voice_name\":\"" + voicename + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode.number} серия" + "</div></div>";
                            firstjson = false;
                        }
                    }
                    #endregion
                }
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }


        #region search
        async ValueTask<int> search(string title, string imdb_id, long kinopoisk_id)
        {
            if (string.IsNullOrWhiteSpace(title))
                return 0;

            if (kinopoisk_id == 0 && string.IsNullOrWhiteSpace(imdb_id))
                return 0;

            string memKey = $"kinopub:search:{title}:{imdb_id}:{kinopoisk_id}";
            if (!memoryCache.TryGetValue(memKey, out JArray items))
            {
                var root = await HttpClient.Get<JObject>($"{AppInit.conf.KinoPub.apihost}/v1/items/search?q={HttpUtility.UrlEncode(title)}&access_token={AppInit.conf.KinoPub.token}&field=title&perpage=200", timeoutSeconds: 8, proxy: proxyManager.Get());
                if (root == null)
                {
                    proxyManager.Refresh();
                    return 0;
                }

                items = root.Value<JArray>("items");
                if (items == null)
                {
                    proxyManager.Refresh();
                    return 0;
                }

                memoryCache.Set(memKey, items, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 40 : 10));
            }

            foreach (var item in items)
            {
                if (item.Value<int?>("kinopoisk") is int _kp && _kp > 0 && _kp == kinopoisk_id)
                    return item.Value<int>("id");

                if ($"tt{item.Value<int?>("imdb")}" == imdb_id)
                    return item.Value<int>("id");
            }

            return 0;
        }
        #endregion
    }
}
