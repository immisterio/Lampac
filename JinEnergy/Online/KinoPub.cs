using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class KinoPubController : BaseController
    {
        [JSInvokable("lite/kinopub")]
        async public static Task<string> Index(string args)
        {
            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int postid = int.Parse(parse_arg("postid", args) ?? "0");
            int clarification = arg.clarification;

            var oninvk = new KinoPubInvoke
            (
               null,
               AppInit.KinoPub.corsHost(),
               AppInit.KinoPub.token,
               ongettourl => JsHttpClient.Get(AppInit.KinoPub.corsHost(ongettourl)),
               onstreamtofile => onstreamtofile
               //AppInit.log
            );

            if (postid == 0)
            {
                if (arg.kinopoisk_id == 0 && string.IsNullOrEmpty(arg.imdb_id))
                    return OnError("arg");

                if (arg.original_language != "en")
                    clarification = 1;

                postid = await InvStructCache(arg.id, $"kinopub:search:{arg.title}:{clarification}:{arg.imdb_id}", () => oninvk.Search(arg.title, arg.original_title, clarification, arg.imdb_id, arg.kinopoisk_id));
                if (postid == 0 || postid == -1)
                    return OnError("postid");
            }

            var root = await InvokeCache(arg.id, $"kinopub:post:{postid}", () => oninvk.Post(postid));
            if (root == null)
                return OnError("root");

            return oninvk.Html(root, AppInit.KinoPub.filetype, arg.title, arg.original_title, postid, s);
        }
    }
}
