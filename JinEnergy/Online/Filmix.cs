using JinEnergy.Engine;
using Lampac.Models.LITE.KinoPub;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class FilmixController : BaseController
    {
        [JSInvokable("lite/filmix")]
        async public static Task<dynamic> Index(string args)
        {
            int s = int.Parse(arg("s", args) ?? "-1");
            int t = int.Parse(arg("t", args) ?? "0");
            int postid = int.Parse(arg("postid", args) ?? "0");
            defaultOnlineArgs(args, out long id, out string? imdb_id, out long kinopoisk_id, out string? title, out string? original_title, out int serial, out string? original_language, out int year, out string? source, out int clarification, out long cub_id, out string? account_email);

            if (original_language != "en")
                clarification = 1;

            var oninvk = new FilmixInvoke
            (
               null,
               AppInit.Filmix.host,
               AppInit.Filmix.token,
               ongettourl => JsHttpClient.Get(ongettourl),
               onstreamtofile => onstreamtofile
               //AppInit.log
            );

            postid = postid == 0 ? await InvStructCache(id, $"filmix:search:{title}:{original_title}:{clarification}", () => oninvk.Search(title, original_title, clarification, year)) : postid;
            if (postid == 0)
                return OnError("postid");

            var player_links = await InvokeCache(id, $"filmix:post:{postid}", () => oninvk.Post(postid));
            if (player_links == null)
                return OnError("player_links");

            return oninvk.Html(player_links, AppInit.Filmix.pro, postid, title, original_title, t, s);
        }
    }
}
