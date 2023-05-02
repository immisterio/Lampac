using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class RedheadsoundController : BaseController
    {
        [JSInvokable("lite/redheadsound")]
        async public static Task<string> Index(string args)
        {
            defaultOnlineArgs(args, out long id, out string? imdb_id, out long kinopoisk_id, out string? title, out string? original_title, out int serial, out string? original_language, out int year, out string? source, out int clarification, out long cub_id, out string? account_email);

            if (original_language != "en")
                clarification = 1;

            var oninvk = new RedheadsoundInvoke
            (
               null,
               AppInit.Redheadsound.corsHost(),
               ongettourl => JsHttpClient.Get(AppInit.Redheadsound.corsHost(ongettourl)),
               (url, data) => JsHttpClient.Post(AppInit.Redheadsound.corsHost(url), data),
               streamfile => streamfile
            );

            var content = await InvokeCache(id, $"redheadsound:view:{title}:{year}:{clarification}", () => oninvk.Embed(clarification == 1 ? title : (original_title ?? title), year));
            if (content == null)
                return string.Empty;

            return oninvk.Html(content, title);
        }
    }
}
