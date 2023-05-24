using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;
using System.Text.RegularExpressions;

namespace JinEnergy.Online
{
    public class FilmixController : BaseController
    {
        [JSInvokable("lite/filmix")]
        async public static Task<string> Index(string args)
        {
            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int t = int.Parse(parse_arg("t", args) ?? "0");
            int postid = int.Parse(parse_arg("postid", args) ?? "0");
            int clarification = arg.clarification;

            if (arg.original_language != "en")
                clarification = 1;

            string? hashfimix = null;
            if (AppInit.Filmix.pro == false && string.IsNullOrEmpty(AppInit.Filmix.token))
                hashfimix = await InvokeCache(0, "filmix:hash", () => JsHttpClient.Get("https://bwa.to/temp/hashfimix.txt", timeoutSeconds: 4));

            var oninvk = new FilmixInvoke
            (
               null,
               AppInit.Filmix.corsHost(),
               AppInit.Filmix.token,
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
                return OnError("player_links");

            return oninvk.Html(player_links, (hashfimix != null ? true : AppInit.Filmix.pro), postid, arg.title, arg.original_title, t, s);
        }


        static string replaceLink(string? hashfimix, string l)
        {
            if (string.IsNullOrEmpty(hashfimix) || l.EndsWith("_480.mp4"))
                return l;

            var filmixservtime = DateTime.UtcNow.AddHours(2).Hour;
            bool hidefree720 = string.IsNullOrWhiteSpace(AppInit.Filmix.token) && filmixservtime >= 19 && filmixservtime <= 23;

            if (!hidefree720 && l.EndsWith("_720.mp4"))
                return l;

            l = Regex.Replace(l, "/s/[^/]+/", hashfimix);
            l = Regex.Replace(l, "^https?://", "");

            return "http://91.201.115.214:5823/" + l;
        }
    }
}
