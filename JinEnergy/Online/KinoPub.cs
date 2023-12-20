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
            var init = AppInit.KinoPub;

            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int postid = int.Parse(parse_arg("postid", args) ?? "0");
            int clarification = arg.clarification;

            var oninvk = new KinoPubInvoke
            (
               null,
               init.corsHost(),
               init.token,
               ongettourl => JsHttpClient.Get(init.cors(ongettourl)),
               streamfile => HostStreamProxy(init, streamfile)
               //AppInit.log
            );

            if (postid == 0)
            {
                if (arg.original_language != "en")
                    clarification = 1;

                var res = await InvStructCache(arg.id, $"kinopub:search:{arg.title}:{clarification}:{arg.imdb_id}", () => oninvk.Search(arg.title, arg.original_title, arg.year, clarification, arg.imdb_id, arg.kinopoisk_id));

                if (res.similars != null)
                    return res.similars;

                postid = res.id;

                if (postid == 0 || postid == -1)
                    return EmptyError("postid");
            }

            var root = await InvokeCache(arg.id, $"kinopub:post:{postid}", () => oninvk.Post(postid));
            if (root == null)
                return EmptyError("root");

            return oninvk.Html(root, init.filetype, arg.title, arg.original_title, postid, s);
        }
    }
}
