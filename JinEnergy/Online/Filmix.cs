using JinEnergy.Engine;
using Lampac.Models.LITE;
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
            var init = AppInit.Filmix.Clone();

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
               init.corsHost(),
               string.IsNullOrEmpty(init.token) ? FilmixInvoke.dmcatoken : init.token,
               ongettourl => JsHttpClient.Get(init.cors(ongettourl)),
               streamfile => HostStreamProxy(init, replaceLink(streamfile))
            );

            if (postid == 0)
            {
                string memkey = $"filmix:search:{arg.title}:{arg.original_title}:{clarification}";
                refresh_similars: var res = await InvStructCache(arg.id, memkey, () => oninvk.Search(arg.title, arg.original_title, clarification, arg.year));

                if (res.id == 0)
                {
                    if (string.IsNullOrEmpty(res.similars))
                    {
                        IMemoryCache.Remove(memkey);

                        if (IsRefresh(init))
                            goto refresh_similars;
                    }

                    return res.similars ?? string.Empty;
                }

                postid = res.id;
            }

            if (lastpostid != postid && oninvk.token == FilmixInvoke.dmcatoken)
            {
                await refreshash(init, postid);
                if (hashfimix == null)
                    oninvk.token = null;
            }

            string mkey = $"filmix:post:{postid}";
            refresh: var player_links = await InvokeCache(arg.id, mkey, () => oninvk.Post(postid));

            string html = oninvk.Html(player_links, init.pro, postid, arg.title, arg.original_title, t, s);
            if (string.IsNullOrEmpty(html))
            {
                IMemoryCache.Remove(mkey);
                if (IsRefresh(init))
                    goto refresh;
            }

            return html;
        }


        async static ValueTask refreshash(FilmixSettings init, int postid)
        {
            lastpostid = postid;

            string? json = await JsHttpClient.Get($"{init.corsHost()}/api/v2/post/2057?user_dev_apk=2.0.1&user_dev_id=&user_dev_name=Xiaomi&user_dev_os=11&user_dev_token=&user_dev_vendor=Xiaomi", timeoutSeconds: 5);
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