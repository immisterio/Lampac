﻿using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Web;
using Newtonsoft.Json.Linq;
using System.Linq;
using Lampac.Engine.CORE;
using Online;
using Shared.Model.Templates;
using Shared.Model.Online;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Lampac.Models.LITE;

namespace Lampac.Controllers.LITE
{
    public class Mirage : BaseOnlineController
    {
        ValueTask<AllohaSettings> Initialization()
        {
            return loadKit(AppInit.conf.Mirage, (j, i, c) =>
            {
                if (j.ContainsKey("m4s"))
                    i.m4s = c.m4s;
                return i;
            });
        }

        [HttpGet]
        [Route("lite/mirage")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int serial, string original_language, int year, int t = -1, int s = -1, bool rjson = false)
        {
            var init = await Initialization();
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            var result = await search(imdb_id, kinopoisk_id, title, serial, original_language, year);
            if (result.category_id == 0 || result.data == null)
                return OnError();

            JToken data = result.data;
            var frame = await iframe(data.Value<string>("token_movie"));
            if (frame.all == null)
                return OnError();

            if (result.category_id is 1 or 3)
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title);

                foreach (var i in frame.all["theatrical"].ToObject<Dictionary<string, Dictionary<string, JObject>>>())
                {
                    var file = i.Value.First().Value;

                    string translation = file.Value<string>("translation");
                    string quality = file.Value<string>("quality");
                    long id = file.Value<long>("id");
                    bool uhd = init.m4s ? file.Value<bool>("uhd") : false;

                    string link = $"{host}/lite/mirage/video?id_file={id}&token_movie={data.Value<string>("token_movie")}&acceptsControls={frame.acceptsControls}";
                    string streamlink = accsArgs($"{link.Replace("/video", "/video.m3u8")}&play=true");

                    mtpl.Append(translation, link, "call", streamlink, voice_name: uhd ? "2160p" : quality, quality: uhd ? "2160p" : "");

                }

                return ContentTo(rjson ? mtpl.ToJson() : mtpl.ToHtml());
                #endregion
            }
            else
            {
                #region Сериал
                string defaultargs = $"&imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&year={year}&original_language={original_language}";

                if (s == -1)
                {
                    #region Сезоны
                    string q = null;
                    try
                    {
                        if (init.m4s)
                        {
                            var voice = frame.all.ToObject<Dictionary<string, Dictionary<string, Dictionary<string, JObject>>>>().First().Value.First().Value.Values.First();
                            q = voice.Value<bool>("uhd") == true ? "2160p" : null;
                        }
                    }
                    catch { }

                    var tpl = new SeasonTpl(q);

                    foreach (var season in frame.all.ToObject<Dictionary<string, object>>())
                        tpl.Append($"{season.Key} сезон", $"{host}/lite/mirage?rjson={rjson}&s={season.Key}{defaultargs}", season.Key);

                    return ContentTo(rjson ? tpl.ToJson() : tpl.ToHtml());
                    #endregion
                }
                else
                {
                    var vtpl = new VoiceTpl();
                    var etpl = new EpisodeTpl();
                    var voices = new HashSet<int>();

                    if (frame.all[s.ToString()] is JArray)
                    {
                        #region Перевод
                        foreach (var episode in frame.all[s.ToString()])
                        {
                            foreach (var voice in episode.ToObject<Dictionary<string, JObject>>().Select(i => i.Value))
                            {
                                int id_translation = voice.Value<int>("id_translation");
                                if (voices.Contains(id_translation))
                                    continue;

                                voices.Add(id_translation);

                                if (t == -1)
                                    t = id_translation;

                                string link = $"{host}/lite/mirage?rjson={rjson}&s={s}&t={id_translation}{defaultargs}";
                                bool active = t == id_translation;

                                vtpl.Append(voice.Value<string>("translation"), active, link);
                            }
                        }
                        #endregion

                        foreach (var episode in frame.all[s.ToString()])
                        {
                            foreach (var voice in episode.ToObject<Dictionary<string, JObject>>().Select(i => i.Value))
                            {
                                if (voice.Value<int>("id_translation") != t)
                                    continue;

                                string translation = voice.Value<string>("translation");
                                int e = voice.Value<int>("episode");

                                string link = $"{host}/lite/mirage/video?id_file={voice.Value<long>("id")}&token_movie={data.Value<string>("token_movie")}&acceptsControls={frame.acceptsControls}";
                                string streamlink = accsArgs($"{link.Replace("/video", "/video.m3u8")}&play=true");

                                if (e > 0)
                                    etpl.Append($"{e} серия", title ?? original_title, s.ToString(), e.ToString(), link, "call", voice_name: translation, streamlink: streamlink);
                            }
                        }
                    }
                    else
                    {
                        #region Перевод
                        foreach (var episode in frame.all[s.ToString()].ToObject<Dictionary<string, Dictionary<string, JObject>>>())
                        {
                            foreach (var voice in episode.Value.Select(i => i.Value))
                            {
                                int id_translation = voice.Value<int>("id_translation");
                                if (voices.Contains(id_translation))
                                    continue;

                                voices.Add(id_translation);

                                if (t == -1)
                                    t = id_translation;

                                string link = $"{host}/lite/mirage?rjson={rjson}&s={s}&t={id_translation}{defaultargs}";
                                bool active = t == id_translation;

                                vtpl.Append(voice.Value<string>("translation"), active, link);
                            }
                        }
                        #endregion

                        foreach (var episode in frame.all[s.ToString()].ToObject<Dictionary<string, Dictionary<string, JObject>>>())
                        {
                            foreach (var voice in episode.Value.Select(i => i.Value))
                            {
                                string translation = voice.Value<string>("translation");
                                if (voice.Value<int>("id_translation") != t)
                                    continue;

                                string link = $"{host}/lite/mirage/video?id_file={voice.Value<long>("id")}&token_movie={data.Value<string>("token_movie")}&acceptsControls={frame.acceptsControls}";
                                string streamlink = accsArgs($"{link.Replace("/video", "/video.m3u8")}&play=true");

                                etpl.Append($"{episode.Key} серия", title ?? original_title, s.ToString(), episode.Key, link, "call", voice_name: translation, streamlink: streamlink);
                            }
                        }
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
        [Route("lite/mirage/video")]
        [Route("lite/mirage/video.m3u8")]
        async public Task<ActionResult> Video(long id_file, string token_movie, string acceptsControls, bool play)
        {
            var init = await Initialization();
            if (await IsBadInitialization(init))
                return badInitMsg;

            string memKey = $"mirage:video:{id_file}:{init.m4s}";
            if (!hybridCache.TryGetValue(memKey, out JToken hlsSource))
            {
                var root = await HttpClient.Post<JObject>($"{init.linkhost}/movie/{id_file}", $"token={init.token}{(init.m4s ? "&av1=true" : "")}&autoplay=0&audio=&subtitle=", httpversion: 2, headers: HeadersModel.Init(
                    ("accepts-controls", $"{acceptsControls}"),
                    ("cache-control", "no-cache"),
                    ("dnt", "1"),
                    ("origin", init.linkhost),
                    ("pragma", "no-cache"),
                    ("priority", "u=1, i"),
                    ("referer", $"{init.linkhost}/?token_movie={token_movie}&token={init.token}"),
                    ("sec-ch-ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\""),
                    ("sec-ch-ua-mobile", "?0"),
                    ("sec-ch-ua-platform", "\"Windows\""),
                    ("sec-fetch-dest", "empty"),
                    ("sec-fetch-mode", "cors"),
                    ("sec-fetch-site", "same-origin"),
                    ("sec-fetch-storage-access", "active"),
                    ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36"),
                    ("x-requested-with", "XMLHttpRequest")
                ));

                if (root == null || !root.ContainsKey("hlsSource"))
                    return null;

                foreach (var item in root["hlsSource"])
                {
                    if (item?.Value<bool>("default") == true)
                    {
                        hlsSource = item;
                        break;
                    }
                }

                if (hlsSource == null)
                    hlsSource = root["hlsSource"].First;

                hybridCache.Set(memKey, hlsSource, cacheTime(15));
            }

            var streamHeaders = HeadersModel.Init(
                ("accepts-controls", $"{acceptsControls}"),
                ("cache-control", "no-cache"),
                ("dnt", "1"),
                ("origin", init.linkhost),
                ("pragma", "no-cache"),
                ("referer", $"{init.linkhost}/"),
                ("sec-ch-ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "cross-site"),
                ("sec-fetch-storage-access", "active"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36")
            );

            var streamquality = new StreamQualityTpl();

            foreach (var q in hlsSource["quality"].ToObject<Dictionary<string, string>>())
            {
                if (!init.m4s && (q.Key is "2160" or "1440"))
                    continue;

                string link = Regex.Match(q.Value, "(https?://[^\n\r\t ]+/[^\\.]+\\.m3u8)").Groups[1].Value;
                streamquality.Append(HostStreamProxy(init, link, headers: streamHeaders), $"{q.Key}p");
            }

            if (play)
                return Redirect(streamquality.Firts().link);

            return ContentTo(VideoTpl.ToJson("play", streamquality.Firts().link, hlsSource.Value<string>("label"), streamquality: streamquality, vast: init.vast));
        }
        #endregion

        #region iframe
        async ValueTask<(JToken all, string acceptsControls)> iframe(string token_movie)
        {
            var init = await Initialization();
            string memKey = $"mirage:iframe:{token_movie}";

            if (!hybridCache.TryGetValue(memKey, out (JToken all, string acceptsControls) cache))
            {
                string html = await HttpClient.Get($"{init.linkhost}/?token_movie={token_movie}&token={init.token}", httpversion: 2, timeoutSeconds: 8, headers: HeadersModel.Init(
                    ("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
                    ("cache-control", "no-cache"),
                    ("dnt", "1"),
                    ("pragma", "no-cache"),
                    ("priority", "u=0, i"),
                    ("referer", "https://kino-2024.org/"),
                    ("sec-ch-ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\""),
                    ("sec-ch-ua-mobile", "?0"),
                    ("sec-ch-ua-platform", "\"Windows\""),
                    ("sec-fetch-dest", "iframe"),
                    ("sec-fetch-mode", "navigate"),
                    ("sec-fetch-site", "cross-site"),
                    ("sec-fetch-storage-access", "active"),
                    ("upgrade-insecure-requests", "1"),
                    ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36")
                ));

                string json = Regex.Match(html ?? "", "fileList = JSON.parse\\('([^\n\r]+)'\\);").Groups[1].Value;
                if (string.IsNullOrEmpty(json))
                    return default;

                string acceptsControls = Regex.Match(html, "name=\"user\" content=\"([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrEmpty(acceptsControls))
                    return default;

                try
                {
                    var root = JsonConvert.DeserializeObject<JObject>(json);
                    if (root == null || !root.ContainsKey("all"))
                        return default;

                    cache = (root["all"], acceptsControls);

                    hybridCache.Set(memKey, cache, cacheTime(40));
                }
                catch { return default; }
            }

            return cache;
        }
        #endregion


        #region search
        async ValueTask<(bool refresh_proxy, int category_id, JToken data)> search(string imdb_id, long kinopoisk_id, string title, int serial, string original_language, int year)
        {
            var init = await Initialization();
            string memKey = $"mirage:view:{kinopoisk_id}:{imdb_id}";
            if (0 >= kinopoisk_id && string.IsNullOrEmpty(imdb_id))
                memKey = $"mirage:viewsearch:{title}:{serial}:{original_language}:{year}";

            JObject root;

            if (!hybridCache.TryGetValue(memKey, out (int category_id, JToken data) res))
            {
                if (memKey.Contains(":viewsearch:"))
                {
                    if (string.IsNullOrWhiteSpace(title) || year == 0)
                        return default;

                    root = await HttpClient.Get<JObject>($"{init.apihost}/?token={init.token}&name={HttpUtility.UrlEncode(title)}&list={(serial == 1 ? "serial" : "movie")}", timeoutSeconds: 8, headers: httpHeaders(init));
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
                    root = await HttpClient.Get<JObject>($"{init.apihost}/?token={init.token}&kp={kinopoisk_id}&imdb={imdb_id}", timeoutSeconds: 8, headers: httpHeaders(init));
                    if (root == null)
                        return (true, 0, null);

                    if (root.ContainsKey("data"))
                    {
                        res.data = root.GetValue("data");
                        res.category_id = res.data.Value<int>("category");
                    }
                }

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
