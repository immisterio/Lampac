using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;
using System.Text.RegularExpressions;

namespace JinEnergy.Online
{
    public class FilmixController : BaseController
    {
        [JSInvokable("lite/filmix")]
        async public static ValueTask<string> Index(string args)
        {
            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int t = int.Parse(parse_arg("t", args) ?? "0");
            int postid = int.Parse(parse_arg("postid", args) ?? "0");
            int clarification = arg.clarification;

            if (arg.original_language != "en")
                clarification = 1;

            var oninvk = new FilmixInvoke
            (
               null,
               AppInit.Filmix.corsHost(),
               (hashfimix != null || (postid == 0 && string.IsNullOrEmpty(AppInit.Filmix.token))) ? "bc170de3b2cafb09283b936011f054ed" : AppInit.Filmix.token,
               ongettourl => JsHttpClient.Get(AppInit.Filmix.corsHost(ongettourl)),
               onstreamtofile => replaceLink(onstreamtofile)
               //AppInit.log
            );

            if (postid == 0)
            {
                var res = await InvStructCache(arg.id, $"filmix:search:{arg.title}:{arg.original_title}:{clarification}", () => oninvk.Search(arg.title, arg.original_title, clarification, arg.year));
                if (res.id == 0)
                    return res.similars;

                postid = res.id;
            }

            await gofreehash(postid);

            var player_links = await InvokeCache(arg.id, $"filmix:post:{postid}", () => oninvk.Post(postid));
            if (player_links == null)
                return EmptyError("player_links");

            return oninvk.Html(player_links, (hashfimix != null ? true : AppInit.Filmix.pro), postid, arg.title, arg.original_title, t, s);
        }



        static string? hashfimix = null;

        static int lastpostid = -1;

        async static ValueTask gofreehash(int postid)
        {
            if (AppInit.Filmix.pro != false || !string.IsNullOrEmpty(AppInit.Filmix.token))
                return;

            if (lastpostid == postid)
                return;

            lastpostid = postid;

            string? FXFS = await JsHttpClient.Get($"https://bwa.to/temp/hashfimix.txt?v={DateTime.Now.ToBinary()}", timeoutSeconds: 4);

            if (string.IsNullOrEmpty(FXFS))
            {
                hashfimix = null;
                return;
            }

            if (hashfimix == null || !hashfimix.StartsWith(FXFS))
            {
                hashfimix = null; // сбиваем хеш

                string[] chars = new string[]
                {
                    "1", "2", "3", "4", "5", "6", "7", "8", "9", "0",
                    "q", "w", "e", "r", "t", "y", "u", "i", "o", "p", "a", "s", "d", "f", "g", "h", "j", "k", "l", "z", "x", "c", "v", "b", "n", "m",
                    "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "A", "S", "D", "F", "G", "H", "J", "K", "L", "Z", "X", "C", "V", "B", "N", "M"
                };

                for (int i = 0; i < 3; i++)
                {
                    string hash = chars[AppInit.random.Next(0, chars.Length)] + chars[AppInit.random.Next(0, chars.Length)] + chars[AppInit.random.Next(0, chars.Length)];

                    if (i > 0)
                        hash += chars[AppInit.random.Next(0, chars.Length)];

                    bool res = await checkHash(FXFS, hash, (i == 0 ? 5 : 3));
                    if (res)
                    {
                        hashfimix = $"{FXFS}{hash}";
                        break;
                    }
                }
            }
        }

        static string replaceLink(string l)
        {
            if (string.IsNullOrEmpty(hashfimix))
                return l;

            return Regex.Replace(l, "/s/[^/]+/", $"/s/{hashfimix}/");
        }

        async static ValueTask<bool> checkHash(string FXFS, string hash, int timeout)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(timeout);
                    client.MaxResponseContentBufferSize = 1_000_000; // 1MB

                    string url = $"https://chache07.werkecdn.me/s/{FXFS}{hash}/UHD_090/Ya.delayu.shag.2023.WEB-DL.1080p_1440.mp4";

                    using (HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), HttpCompletionOption.ResponseHeadersRead))
                    {
                        if (((int)response.StatusCode) is 429 or 404 or 400)
                            return false;

                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }
    }
}