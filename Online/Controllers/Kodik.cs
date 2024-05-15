using System;
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
                (uri, head) => HttpClient.Get(init.cors(uri), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
                (uri, data) => HttpClient.Post(init.cors(uri), data, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
                streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "kodik"),
                requesterror: () => proxyManager.Refresh()
            );
        }
        #endregion

        [HttpGet]
        [Route("lite/kodik")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int clarification, string pick, string kid, int s = -1)
        {
            if (!AppInit.conf.Kodik.enable)
                return OnError();

            List<Result> content = null;
            var oninvk = InitKodikInvoke();

            if (clarification == 1)
            {
                if (string.IsNullOrEmpty(title))
                    return OnError();

                var res = await InvokeCache($"kodik:search:{title}", cacheTime(40, init: AppInit.conf.Kodik), () => oninvk.Embed(title), proxyManager);
                if (res?.result == null)
                    return OnError();

                if (res.result.Count == 0)
                    return OnError();

                if (string.IsNullOrEmpty(pick))
                    return Content(res.html ?? string.Empty, "text/html; charset=utf-8");

                content = oninvk.Embed(res.result, pick);
            }
            else
            {
                if (kinopoisk_id == 0 && string.IsNullOrEmpty(imdb_id))
                    return OnError();

                content = await InvokeCache($"kodik:search:{kinopoisk_id}:{imdb_id}", cacheTime(40, init: AppInit.conf.Kodik), () => oninvk.Embed(imdb_id, kinopoisk_id, s), proxyManager);
                if (content == null)
                    return OnError();

                if (content.Count == 0)
                    return OnError();
            }

            return Content(oninvk.Html(content, imdb_id, kinopoisk_id, title, original_title, clarification, pick, kid, s, true), "text/html; charset=utf-8");
        }

        #region Video - API
        [HttpGet]
        [Route("lite/kodik/video")]
        [Route("lite/kodik/video.m3u8")]
        async public Task<ActionResult> VideoAPI(string title, string original_title, string link, int episode, string account_email, bool play)
        {
            var init = AppInit.conf.Kodik;

            if (!init.enable)
                return OnError();

            if (string.IsNullOrWhiteSpace(init.secret_token))
            {
                string uri = play ? "videoparse.m3u8" : "videoparse";
                return LocalRedirect($"/lite/kodik/{uri}?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&link={HttpUtility.UrlEncode(link)}&episode={episode}&account_email={HttpUtility.UrlEncode(account_email)}&play={play}");
            }

            string userIp = HttpContext.Connection.RemoteIpAddress.ToString();
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

            string streansquality = string.Empty;
            foreach (var l in streams)
            {
                string hls = HostStreamProxy(init, l.url, proxy: proxy, plugin: "kodik");
                streansquality += $"\"{l.q}\":\"" + hls + "\",";
            }

            string name = title ?? original_title;
            if (episode > 0)
                name += $" ({episode} серия)";

            string url = HostStreamProxy(init, streams[0].url, proxy: proxy, plugin: "kodik");

            if (play)
                return Redirect(url);

            return Content("{\"method\":\"play\",\"url\":\"" + url + "\",\"title\":\"" + name + "\", \"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}}", "application/json; charset=utf-8");
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

            var oninvk = InitKodikInvoke();

            var streams = await InvokeCache($"kodik:video:{link}:{play}", cacheTime(40, init: init), () => oninvk.VideoParse(init.linkhost, link), proxyManager);
            if (streams == null)
                return OnError();

            string result = oninvk.VideoParse(streams, title, original_title, episode, play);
            if (string.IsNullOrEmpty(result))
                return OnError();

            if (play)
                return Redirect(result);

            return Content(result, "application/json; charset=utf-8");
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
