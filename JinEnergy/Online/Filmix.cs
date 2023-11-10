using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

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
               AppInit.Filmix.token,
               ongettourl => JsHttpClient.Get(AppInit.Filmix.corsHost(ongettourl)),
               onstreamtofile => onstreamtofile
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

            return oninvk.Html(player_links, AppInit.Filmix.pro, postid, arg.title, arg.original_title, t, s);
        }
    }
}
