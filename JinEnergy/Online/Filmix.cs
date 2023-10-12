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

            string? hashfimix = await InvokeCache(0, "filmix:hash", gofreehash);

            var oninvk = new FilmixInvoke
            (
               null,
               AppInit.Filmix.corsHost(),
               hashfimix != null ? "bc170de3b2cafb09283b936011f054ed" : AppInit.Filmix.token,
               ongettourl => JsHttpClient.Get(AppInit.Filmix.corsHost(ongettourl)),
               onstreamtofile => replaceLink(hashfimix, onstreamtofile)
               //AppInit.log
            );

            if (postid == 0)
            {
                var res = await InvStructCache(arg.id, $"filmix:search:{arg.title}:{arg.original_title}:{clarification}", () => oninvk.Search(arg.title, arg.original_title, clarification, arg.year));
                if (res.id == 0)
                    return res.similars;

                postid = res.id;
            }

            var player_links = await InvokeCache(arg.id, $"filmix:post:{postid}", () => oninvk.Post(postid));
            if (player_links == null)
                return EmptyError("player_links");

            return oninvk.Html(player_links, (hashfimix != null ? true : AppInit.Filmix.pro), postid, arg.title, arg.original_title, t, s);
        }



        static Random random = new Random();

        async static ValueTask<string?> gofreehash()
        {
            if (AppInit.Filmix.pro != false || !string.IsNullOrEmpty(AppInit.Filmix.token))
                return null;

            string? hashfimix = null;

            string? FXFS = await JsHttpClient.Get($"https://bwa.to/temp/hashfimix.txt?v={DateTime.Now.ToBinary()}", timeoutSeconds: 2);

            if (!string.IsNullOrEmpty(FXFS))
            {
                string[] chars = new string[]
                {
                    "1", "2", "3", "4", "5", "6", "7", "8", "9", "0",
                    "q", "w", "e", "r", "t", "y", "u", "i", "o", "p", "a", "s", "d", "f", "g", "h", "j", "k", "l", "z", "x", "c", "v", "b", "n", "m",
                    "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "A", "S", "D", "F", "G", "H", "J", "K", "L", "Z", "X", "C", "V", "B", "N", "M"
                };

                for (int i = 0; i < 2; i++)
                {
                    string hash = chars[random.Next(0, chars.Length)] + chars[random.Next(0, chars.Length)] + chars[random.Next(0, chars.Length)];
                    bool res = await checkHash(FXFS, hash);
                    if (res)
                    {
                        hashfimix = $"{FXFS}{hash}";
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(hashfimix))
                return hashfimix;

            return null;
        }

        static string replaceLink(string? hashfimix, string l)
        {
            if (string.IsNullOrEmpty(hashfimix))
                return l;

            return Regex.Replace(l, "/s/[^/]+/", $"/s/{hashfimix}/");
        }

        async static ValueTask<bool> checkHash(string FXFS, string hash)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(7);
                    client.MaxResponseContentBufferSize = 1_000_000; // 1MB

                    string url = $"http://nl201.cdnsqu.com/s/{FXFS}{hash}/HD_56/Mission.Impossible.CLEAN.2023_1080.mp4";

                    using (HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), HttpCompletionOption.ResponseHeadersRead))
                    {
                        if (((int)response.StatusCode) is 429 or 404 or  400)
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
