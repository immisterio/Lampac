using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Web;
using Newtonsoft.Json.Linq;
using System.Linq;
using Lampac.Engine.CORE;
using Lampac.Models.LITE.Alloha;
using Online;
using Shared.Engine.CORE;
using Shared.Model.Templates;
using Shared.Model.Online.Alloha;

namespace Lampac.Controllers.LITE
{
    public class Alloha : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager("alloha", AppInit.conf.Alloha);

        [HttpGet]
        [Route("lite/alloha")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int serial, string original_language, int year, string t, int s = -1, bool origsource = false, bool rjson = false)
        {
            var init = AppInit.conf.Alloha;
            if (!init.enable)
                return OnError("disable");

            if (init.rhub)
                return ShowError(RchClient.ErrorMsg);

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            var result = await search(imdb_id, kinopoisk_id, title, serial, original_language, year);
            if (result.category_id == 0)
                return OnError("data", proxyManager, result.refresh_proxy);

            if (result.data == null)
                return Ok();

            if (origsource)
                return Json(result.data);

            JToken data = result.data;

            string defaultargs = $"&imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&serial={serial}&year={year}&original_language={original_language}";

            if (result.category_id is 1 or 3)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title);

                foreach (var translation in data.Value<JObject>("translation_iframe").ToObject<Dictionary<string, Dictionary<string, object>>>())
                {
                    string link = $"{host}/lite/alloha/video?t={translation.Key}" + defaultargs;
                    string streamlink = accsArgs($"{link.Replace("/video", "/video.m3u8")}&play=true");

                    bool uhd = translation.Value["uhd"].ToString() == "True" && AppInit.conf.Alloha.m4s;
                    mtpl.Append(translation.Value["name"].ToString(), link, "call", streamlink, voice_name: uhd ? "2160p" : translation.Value["quality"].ToString(), quality: uhd ? "2160p" : "");
                }

                return ContentTo(rjson ? mtpl.ToJson() : mtpl.ToHtml());
                #endregion
            }
            else
            {
                #region Сериал
                if (s == -1)
                {
                    var tpl = new SeasonTpl(result.data.Value<bool>("uhd") && AppInit.conf.Alloha.m4s ? "2160p" : null);

                    foreach (var season in data.Value<JObject>("seasons").ToObject<Dictionary<string, object>>().Reverse())
                        tpl.Append($"{season.Key} сезон", $"{host}/lite/alloha?rjson={rjson}&s={season.Key}{defaultargs}", season.Key);

                    return ContentTo(rjson ? tpl.ToJson() : tpl.ToHtml());
                }
                else
                {
                    #region Перевод
                    var vtpl = new VoiceTpl();
                    var temp_translation = new HashSet<string>();

                    string activTranslate = t;

                    foreach (var episodes in data.Value<JObject>("seasons").GetValue(s.ToString()).Value<JObject>("episodes").ToObject<Dictionary<string, Episode>>().Select(i => i.Value.translation))
                    {
                        foreach (var translation in episodes)
                        {
                            if (temp_translation.Contains(translation.Value.translation) || translation.Value.translation.ToLower().Contains("субтитры"))
                                continue;

                            temp_translation.Add(translation.Value.translation);

                            if (string.IsNullOrWhiteSpace(activTranslate))
                                activTranslate = translation.Key;

                            vtpl.Append(translation.Value.translation, activTranslate == translation.Key, $"{host}/lite/alloha?rjson={rjson}&s={s}&t={translation.Key}{defaultargs}");
                        }
                    }
                    #endregion

                    var etpl = new EpisodeTpl();

                    foreach (var episode in data.Value<JObject>("seasons").GetValue(s.ToString()).Value<JObject>("episodes").ToObject<Dictionary<string, Episode>>().Reverse())
                    {
                        if (!string.IsNullOrWhiteSpace(activTranslate) && !episode.Value.translation.ContainsKey(activTranslate))
                            continue;

                        string link = $"{host}/lite/alloha/video?t={activTranslate}&s={s}&e={episode.Key}" + defaultargs;
                        string streamlink = accsArgs($"{link.Replace("/video", "/video.m3u8")}&play=true");

                        etpl.Append($"{episode.Key} серия", title ?? original_title, s.ToString(), episode.Key, link, "call", streamlink: streamlink);
                    }

                    if (rjson)
                        return ContentTo(etpl.ToJson(vtpl));

                    return ContentTo(vtpl.ToHtml() + etpl.ToHtml());
                }
                #endregion
            }
        }


        #region Video
        [HttpGet]
        [Route("lite/alloha/video")]
        [Route("lite/alloha/video.m3u8")]
        async public Task<ActionResult> Video(string imdb_id, long kinopoisk_id, string title, string original_title, string t, int s, int e, bool play)
        {
            var init = AppInit.conf.Alloha;
            if (!init.enable)
                return OnError("disable");

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

            string userIp = HttpContext.Connection.RemoteIpAddress.ToString();
            if (init.localip || init.streamproxy)
            {
                userIp = await mylocalip();
                if (userIp == null)
                    return OnError("userIp");
            }

            string memKey = $"alloha:view:stream:{imdb_id}:{kinopoisk_id}:{t}:{s}:{e}:{userIp}";
            if (!hybridCache.TryGetValue(memKey, out JToken data))
            {
                #region url запроса
                string uri = $"{init.linkhost}/link_file.php?secret_token={init.secret_token}&imdb={imdb_id}&kp={kinopoisk_id}";

                uri += $"&ip={userIp}&translation={t}";

                if (s > 0)
                    uri += $"&season={s}";

                if (e > 0)
                    uri += $"&episode={e}";
                #endregion

                var root = await HttpClient.Get<JObject>(uri, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));
                if (root == null)
                    return OnError("json", proxyManager);

                if (!root.ContainsKey("data"))
                    return OnError("data");

                proxyManager.Success();

                data = root["data"];
                hybridCache.Set(memKey, data, cacheTime(10, init: init));
            }

            bool uhd = data.Value<bool>("4k");
            string default_audio = data.Value<string>("default_audio");

            #region subtitle
            var subtitles = new SubtitleTpl();

            try
            {
                foreach (var sub in data["subtitle"])
                    subtitles.Append(sub.Value<string>("label"), sub.Value<string>("url"));
            }
            catch { }
            #endregion

            var default_streams = new List<(string link, string quality)>() { Capacity = 6 };
            var streams = new List<(string link, string quality)>() { Capacity = 6 };

            foreach (var froot in data["file"])
            {
                void SetVideo(List<(string link, string quality)> list)
                {
                    foreach (var file in froot["url"].ToObject<Dictionary<string, FileQ>>())
                    {
                        string av1 = file.Value.av1;
                        string h264 = file.Value.h264;

                        if (uhd && init.m4s && !string.IsNullOrEmpty(av1) && (file.Key is "2160p" or "1440p"))
                        {
                            list.Add((HostStreamProxy(init, av1, proxy: proxyManager.Get(), plugin: "alloha"), file.Key));
                        }
                        else
                        {
                            string _stream = string.IsNullOrEmpty(h264) ? av1 : h264;

                            if (!string.IsNullOrEmpty(_stream))
                                list.Add((HostStreamProxy(init, _stream, proxy: proxyManager.Get(), plugin: "alloha"), file.Key));
                        }
                    }
                }

                if (default_streams.Count == 0)
                    SetVideo(default_streams);

                if (!string.IsNullOrEmpty(default_audio))
                {
                    string audio = froot.Value<string>("audio");
                    if (string.IsNullOrEmpty(audio) || audio != default_audio)
                        continue;
                }

                SetVideo(streams);
                break;
            }

            if (streams.Count == 0)
                streams = default_streams;

            if (play)
                return Redirect(streams[0].link);

            string streansquality = "\"quality\": {" + string.Join(",", streams.Select(s => $"\"{s.quality}\":\"{s.link}\"")) + "}";
            return Content("{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + (title ?? original_title) + "\", \"subtitles\":" + subtitles.ToJson() + ", " + streansquality + "}", "application/json; charset=utf-8");
        }
        #endregion

        #region search
        async ValueTask<(bool refresh_proxy, int category_id, JToken data)> search(string imdb_id, long kinopoisk_id, string title, int serial, string original_language, int year)
        {
            var init = AppInit.conf.Alloha;

            string memKey = $"alloha:view:{kinopoisk_id}:{imdb_id}";
            if (0 >= kinopoisk_id && string.IsNullOrEmpty(imdb_id))
                memKey = $"alloha:viewsearch:{title}:{serial}:{original_language}:{year}";

            JObject root;

            if (!hybridCache.TryGetValue(memKey, out (int category_id, JToken data) res))
            {
                if (memKey.Contains(":viewsearch:"))
                {
                    if (string.IsNullOrWhiteSpace(title) || year == 0)
                        return default;

                    root = await HttpClient.Get<JObject>($"{init.apihost}/?token={init.token}&name={HttpUtility.UrlEncode(title)}&list={(serial == 1 ? "serial" : "movie")}", timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));
                    if (root == null)
                        return (true, 0, null);

                    if (root.ContainsKey("data"))
                    {
                        foreach (var item in root["data"])
                        {
                            if (item.Value<string>("name")?.ToLower()?.Trim() == title.ToLower())
                            {
                                int y = item.Value<int>("year");
                                if (y > 0 && (y == year || y == (year - 1) || y == (year + 1)))
                                {
                                    if (original_language == "ru" && item.Value<string>("country")?.ToLower() != "россия")
                                        continue;

                                    res.data = item;
                                    res.category_id = item.Value<int>("category_id");
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    root = await HttpClient.Get<JObject>($"{init.apihost}/?token={init.token}&kp={kinopoisk_id}&imdb={imdb_id}", timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));
                    if (root == null)
                        return (true, 0, null);

                    if (root.ContainsKey("data"))
                    {
                        res.data = root.GetValue("data");
                        res.category_id = res.data.Value<int>("category");
                    }
                }

                if (res.data != null)
                    proxyManager.Success();

                if (res.data != null || (root.ContainsKey("error_info") && root.Value<string>("error_info") == "not movie"))
                    hybridCache.Set(memKey, res, cacheTime(res.category_id is 1 or 3 ? 120 : 40, init: init));
                else
                    hybridCache.Set(memKey, res, cacheTime(2, init: init));
            }

            return (false, res.category_id, res.data);
        }
        #endregion
    }
}
