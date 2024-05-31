using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;
using Shared.Model.Online;
using Shared.Model.Online.VDBmovies;
using System.Text.RegularExpressions;

namespace JinEnergy.Online
{
    public class VDBmoviesController : BaseController
    {
        [JSInvokable("lite/vdbmovies")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.VDBmovies.Clone();

            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int sid = int.Parse(parse_arg("sid", args) ?? "-1");

            if (arg.kinopoisk_id == 0)
                return EmptyError("arg");

            var oninvk = new VDBmoviesInvoke
            (
                null,
                MaybeInHls(init.hls, init),
                streamfile => HostStreamProxy(init, streamfile)
            );

            EmbedModel? root = await InvokeCache(arg.id, $"cdnmoviesdb:json:{arg.kinopoisk_id}", async () =>
            {
                if (!AppInit.IsAndrod)
                    await AppInit.JSRuntime!.InvokeAsync<object>("eval", "$('head meta[name=\"referrer\"]').attr('content', 'origin');");

                string? html = await JsHttpClient.Get($"{init.corsHost()}/kinopoisk/{arg.kinopoisk_id}/iframe", HeadersModel.Init(
                    ("Origin", "https://cdnmovies.net"),
                    ("Referer", "https://cdnmovies.net/")
                ));

                if (!AppInit.IsAndrod)
                    AppInit.JSRuntime?.InvokeAsync<object>("eval", "$('head meta[name=\"referrer\"]').attr('content', 'no-referrer');");

                string file = Regex.Match(html ?? "", "file: ?'(#[^&']+)").Groups[1].Value;
                if (string.IsNullOrEmpty(file))
                    return null;

                try
                {
                    return oninvk.Embed(await JSRuntime!.InvokeAsync<string?>("eval", oninvk.EvalCode(file)));
                }
                catch
                {
                    return null;
                }
            });

            if (root == null)
                return EmptyError("root");

            return oninvk.Html(root, arg.kinopoisk_id, arg.title, arg.original_title, parse_arg("t", args), s, sid);
        }
    }
}
