﻿using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Web;
using Lampac.Engine.CORE;
using System.Security.Cryptography;
using System.Text;
using Shared.Engine.CORE;
using Online;
using Shared.Engine.Online;
using Shared.Model.Online.Kodik;
using Shared.Model.Templates;

namespace Lampac.Controllers.LITE
{
    public class Kodik : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager("kodik", AppInit.conf.Kodik);

        #region InitKodikInvoke
        public KodikInvoke InitKodikInvoke()
        {
            var proxy = proxyManager.Get();
            var init = AppInit.conf.Kodik;

            return new KodikInvoke
            (
                host,
                init.apihost,
                init.token,
                init.hls,
                string.IsNullOrEmpty(init.secret_token) ? "videoparse" : "video",
                (uri, head) => HttpClient.Get(init.cors(uri), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
                (uri, data) => HttpClient.Post(init.cors(uri), data, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
                streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "kodik"),
                requesterror: () => proxyManager.Refresh()
            );
        }
        #endregion

        [HttpGet]
        [Route("lite/kodik")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int clarification, string pick, string kid, int s = -1, bool rjson = false)
        {
            var init = AppInit.conf.Kodik;
            if (!init.enable)
                return OnError();

            if (init.rhub)
                return ShowError(RchClient.ErrorMsg);

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            List<Result> content = null;
            var oninvk = InitKodikInvoke();

            if (clarification == 1 || (kinopoisk_id == 0 && string.IsNullOrEmpty(imdb_id)))
            {
                EmbedModel res = null;

                if (clarification == 1)
                {
                    if (string.IsNullOrEmpty(title))
                        return OnError();

                    res = await InvokeCache($"kodik:search:{title}", cacheTime(40, init: init), () => oninvk.Embed(title, null, clarification), proxyManager);
                    if (res?.result == null || res.result.Count == 0)
                        return OnError();
                }
                else
                {
                    if (string.IsNullOrEmpty(pick) && string.IsNullOrEmpty(title ?? original_title))
                        return OnError();

                    res = await InvokeCache($"kodik:search2:{original_title}:{title}:{clarification}", cacheTime(40, init: AppInit.conf.Kodik), async () => 
                    {
                        var i = await oninvk.Embed(null, original_title, clarification);
                        if (i?.result == null || i.result.Count == 0)
                            return await oninvk.Embed(title, null, clarification);

                        return i;

                    }, proxyManager);
                }

                if (string.IsNullOrEmpty(pick))
                    return ContentTo(res?.stpl == null ? string.Empty : (rjson ? res.stpl.ToJson() : res.stpl.ToHtml()));

                content = oninvk.Embed(res.result, pick);
            }
            else
            {
                content = await InvokeCache($"kodik:search:{kinopoisk_id}:{imdb_id}", cacheTime(40, init: AppInit.conf.Kodik), () => oninvk.Embed(imdb_id, kinopoisk_id, s), proxyManager);
                if (content == null || content.Count == 0)
                    return LocalRedirect(accsArgs($"/lite/kodik?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}"));
            }

            return ContentTo(oninvk.Html(content, accsArgs(string.Empty), imdb_id, kinopoisk_id, title, original_title, clarification, pick, kid, s, true, rjson));
        }

        #region Video - API
        [HttpGet]
        [Route("lite/kodik/video")]
        [Route("lite/kodik/video.m3u8")]
        async public Task<ActionResult> VideoAPI(string title, string original_title, string link, int episode, bool play)
        {
            var init = AppInit.conf.Kodik;
            if (!init.enable)
                return OnError();

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

            if (string.IsNullOrWhiteSpace(init.secret_token))
            {
                string uri = play ? "videoparse.m3u8" : "videoparse";
                return LocalRedirect(accsArgs($"/lite/kodik/{uri}?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&link={HttpUtility.UrlEncode(link)}&episode={episode}&play={play}"));
            }

            string userIp = requestInfo.IP;
            if (init.localip)
            {
                userIp = await mylocalip();
                if (userIp == null)
                    return OnError();
            }

            var proxy = proxyManager.Get();

            string memKey = $"kodik:view:stream:{link}";
            if (!hybridCache.TryGetValue(memKey, out List<(string q, string url)> streams))
            {
                string deadline = DateTime.Now.AddHours(1).ToString("yyyy MM dd HH").Replace(" ", "");
                string hmac = HMAC(init.secret_token, $"{link}:{userIp}:{deadline}");

                string json = await HttpClient.Get($"{init.linkhost}/api/video-links" + $"?link={link}&p={init.token}&ip={userIp}&d={deadline}&s={hmac}", timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));

                streams = new List<(string q, string url)>();
                var match = new Regex("\"([0-9]+)p?\":{\"Src\":\"(https?:)?//([^\"]+)\"", RegexOptions.IgnoreCase).Match(json);
                while (match.Success)
                {
                    if (!string.IsNullOrWhiteSpace(match.Groups[3].Value))
                        streams.Insert(0, ($"{match.Groups[1].Value}p", $"https://{match.Groups[3].Value}"));

                    match = match.NextMatch();
                }

                if (streams.Count == 0)
                {
                    proxyManager.Refresh();
                    return Content(string.Empty);
                }

                proxyManager.Success();
                hybridCache.Set(memKey, streams, cacheTime(20, init: init));
            }

            var streamquality = new StreamQualityTpl();
            foreach (var l in streams)
                streamquality.Append(HostStreamProxy(init, l.url, proxy: proxy, plugin: "kodik"), l.q);

            if (play)
                return Redirect(streamquality.Firts().link);

            string name = title ?? original_title;
            if (episode > 0)
                name += $" ({episode} серия)";

            return ContentTo(VideoTpl.ToJson("play", streamquality.Firts().link, name, streamquality: streamquality, vast: init.vast));
        }
        #endregion

        #region Video - Parse
        [HttpGet]
        [Route("lite/kodik/videoparse")]
        [Route("lite/kodik/videoparse.m3u8")]
        async public Task<ActionResult> VideoParse(string title, string original_title, string link, int episode, bool play)
        {
            var init = AppInit.conf.Kodik;
            if (!init.enable)
                return OnError();

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

            var oninvk = InitKodikInvoke();

            var streams = await InvokeCache($"kodik:video:{link}:{play}", cacheTime(40, init: init), () => oninvk.VideoParse(init.linkhost, link), proxyManager);
            if (streams == null)
                return OnError();

            string result = oninvk.VideoParse(streams, title, original_title, episode, play, vast: init.vast);
            if (string.IsNullOrEmpty(result))
                return OnError();

            if (play)
                return Redirect(result);

            return ContentTo(result);
        }
        #endregion


        #region HMAC
        static string HMAC(string key, string message)
        {
            using (var hash = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
            {
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(message))).Replace("-", "").ToLower();
            }
        }
        #endregion
    }
}
