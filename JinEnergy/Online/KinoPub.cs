using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class KinoPubController : BaseController
    {
        [JSInvokable("lite/kinopub")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.KinoPub.Clone();

            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int postid = int.Parse(parse_arg("postid", args) ?? "0");

            var oninvk = new KinoPubInvoke
            (
               null,
               init.corsHost(),
               init.token,
               ongettourl => JsHttpClient.Get(init.cors(ongettourl), httpHeaders(args, init)),
               (stream, filepath) => HostStreamProxy(init, stream)
               //AppInit.log
            );

            if (postid == 0)
            {
                string memkey = $"kinopub:search:{arg.title}:{arg.clarification}:{arg.imdb_id}";
                refresh_similars: var res = await InvokeCache(arg.id, memkey, () => oninvk.Search(arg.title, arg.original_title, arg.year, arg.clarification, arg.imdb_id, arg.kinopoisk_id));

                if (!string.IsNullOrEmpty(res?.similars))
                    return res.similars;

                postid = res == null ? 0 : res.id;

                if (postid == 0)
                {
                    IMemoryCache.Remove(memkey);

                    if (IsRefresh(init))
                        goto refresh_similars;

                    return EmptyError("postid");
                }
            }

            string mkey = $"kinopub:post:{postid}";
            refresh: var root = await InvokeCache(arg.id, mkey, () => oninvk.Post(postid));

            string html = oninvk.Html(root, init.filetype, arg.title, arg.original_title, postid, s);
            if (string.IsNullOrEmpty(html))
            {
                IMemoryCache.Remove(mkey);
                if (IsRefresh(init))
                    goto refresh;
            }

            return html;
        }
    }
}
