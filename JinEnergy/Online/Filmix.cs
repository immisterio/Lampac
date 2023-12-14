using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;
using System.Text.RegularExpressions;

namespace JinEnergy.Online
{
    public class FilmixController : BaseController
    {
        static string? hashfimix = null;
        static int lastpostid = -1;

        [JSInvokable("lite/filmix")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.Filmix;

            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int t = int.Parse(parse_arg("t", args) ?? "0");
            int postid = int.Parse(parse_arg("postid", args) ?? "0");
            int clarification = arg.clarification;

            if (arg.original_language != "en")
                clarification = 1;

            string dmcatoken = "bc170de3b2cafb09283b936011f054ed";

            var oninvk = new FilmixInvoke
            (
               null,
               init.corsHost(),
               string.IsNullOrEmpty(init.token) ? dmcatoken : init.token,
               ongettourl => JsHttpClient.Get(init.cors(ongettourl)),
               replaceLink
            );

            if (postid == 0)
            {
                var res = await InvStructCache(arg.id, $"filmix:search:{arg.title}:{arg.original_title}:{clarification}", () => oninvk.Search(arg.title, arg.original_title, clarification, arg.year));
                if (res.id == 0)
                    return res.similars;

                postid = res.id;
            }

            if (lastpostid != postid && oninvk.token == dmcatoken)
            {
                await refreshash(postid);
                if (hashfimix == null)
                    oninvk.token = null;
            }

            var player_links = await InvokeCache(arg.id, $"filmix:post:{postid}", () => oninvk.Post(postid));
            if (player_links == null)
                return EmptyError("player_links");

            return oninvk.Html(player_links, init.pro, postid, arg.title, arg.original_title, t, s);
        }


        async static ValueTask refreshash(int postid)
        {
            lastpostid = postid;

            string? json = await JsHttpClient.Get($"{AppInit.Filmix.corsHost()}/api/v2/post/2057?user_dev_apk=2.0.1&user_dev_id=&user_dev_name=Xiaomi&user_dev_os=11&user_dev_token=&user_dev_vendor=Xiaomi", timeoutSeconds: 4);
            string hash = Regex.Match(json ?? "", "/s\\\\/([^\\/]+)\\\\/").Groups[1].Value;

            if (string.IsNullOrEmpty(hash))
            {
                hashfimix = null;
                return;
            }

            hashfimix = hash;
        }

        static string replaceLink(string l)
        {
            if (hashfimix == null)
                return l;

            return Regex.Replace(l, "/s/[^/]+/", $"/s/{hashfimix}/");
        }
    }
}