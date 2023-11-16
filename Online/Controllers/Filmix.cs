using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Newtonsoft.Json.Linq;
using Shared.Engine.Online;
using Online;
using Shared.Engine.CORE;
using System.Text.RegularExpressions;
using System;

namespace Lampac.Controllers.LITE
{
    public class Filmix : BaseOnlineController
    {
        #region filmixpro
        [HttpGet]
        [Route("lite/filmixpro")]
        async public Task<ActionResult> Pro()
        {
            var token_request = await HttpClient.Get<JObject>($"{AppInit.conf.Filmix.corsHost()}/api/v2/token_request?user_dev_apk=2.0.1&user_dev_id=&user_dev_name=Xiaomi&user_dev_os=11&user_dev_vendor=Xiaomi&user_dev_token=");

            string html = "1. Откройте <a href='https://filmix.ac/consoles'>https://filmix.ac/consoles</a> <br>";
            html += $"2. Введите код <b>{token_request.Value<string>("user_code")}</b><br>";
            html += $"<br><br>В init.conf<br>";
            html += $"1. Укажите token <b>{token_request.Value<string>("code")}</b><br>";
            html += $"2. Измените \"pro\": false, на \"pro\": true, если у вас PRO аккаунт</b>";

            return Content(html, "text/html; charset=utf-8");
        }
        #endregion

        [HttpGet]
        [Route("lite/filmix")]
        async public Task<ActionResult> Index(string title, string original_title, int clarification, string original_language, int year, int postid, int t, int s = -1)
        {
            if (!AppInit.conf.Filmix.enable)
                return OnError();

            if (original_language != "en")
                clarification = 1;

            var proxyManager = new ProxyManager("filmix", AppInit.conf.Filmix);
            var proxy = proxyManager.Get();

            var oninvk = new FilmixInvoke
            (
               host,
               AppInit.conf.Filmix.corsHost(),
               (hashfimix != null || (postid == 0 && string.IsNullOrEmpty(AppInit.conf.Filmix.token))) ? "bc170de3b2cafb09283b936011f054ed" : AppInit.conf.Filmix.token,
               ongettourl => HttpClient.Get(AppInit.conf.Filmix.corsHost(ongettourl), timeoutSeconds: 8, proxy: proxy),
               onstreamtofile => HostStreamProxy(AppInit.conf.Filmix, replaceLink(onstreamtofile), proxy: proxy)
            );

            if (postid == 0)
            {
                var res = await InvokeCache($"filmix:search:{title}:{original_title}:{clarification}", AppInit.conf.multiaccess ? 40 : 10, () => oninvk.Search(title, original_title, clarification, year));
                if (res.id == 0)
                    return Content(res.similars);

                postid = res.id;
            }

            await gofreehash(postid);

            var player_links = await InvokeCache($"filmix:post:{postid}", AppInit.conf.multiaccess ? 20 : 5, () => oninvk.Post(postid));
            if (player_links == null)
                return OnError(proxyManager);

            return Content(oninvk.Html(player_links, (hashfimix != null ? true : AppInit.conf.Filmix.pro), postid, title, original_title, t, s), "text/html; charset=utf-8");
        }




        static Random random = new Random();

        static string hashfimix = null;

        static int lastpostid = -1;

        async static ValueTask gofreehash(int postid)
        {
            if (AppInit.conf.Filmix.pro != false || !string.IsNullOrEmpty(AppInit.conf.Filmix.token))
                return;

            if (lastpostid == postid)
                return;

            lastpostid = postid;

            string FXFS = await HttpClient.Get($"https://bwa.to/temp/hashfimix.txt?v={DateTime.Now.ToBinary()}", timeoutSeconds: 8);

            if (string.IsNullOrEmpty(FXFS))
            {
                hashfimix = null;
                return;
            }

            if (hashfimix == null || !hashfimix.StartsWith(FXFS))
            {
                string[] chars = new string[]
                {
                    "1", "2", "3", "4", "5", "6", "7", "8", "9", "0",
                    "q", "w", "e", "r", "t", "y", "u", "i", "o", "p", "a", "s", "d", "f", "g", "h", "j", "k", "l", "z", "x", "c", "v", "b", "n", "m",
                    "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "A", "S", "D", "F", "G", "H", "J", "K", "L", "Z", "X", "C", "V", "B", "N", "M"
                };

                string hash = chars[random.Next(0, chars.Length)] + chars[random.Next(0, chars.Length)] + chars[random.Next(0, chars.Length)] + chars[random.Next(0, chars.Length)];
                hashfimix = $"{FXFS}{hash}";
            }
        }

        static string replaceLink(string l)
        {
            if (string.IsNullOrEmpty(hashfimix))
                return l;

            return Regex.Replace(l, "/s/[^/]+/", $"/s/{hashfimix}/");
        }
    }
}
