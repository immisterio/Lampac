using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;
using Shared.Model.Online;
using System.Text.Json;

namespace JinEnergy.Online
{
    public class VideoDBController : BaseController
    {
        static List<HeadersModel> baseheader = HeadersModel.Init(
            ("cache-control", "no-cache"),
            ("dnt", "1"),
            ("origin", "https://kinoplay2.site"),
            ("pragma", "no-cache"),
            ("priority", "u=1, i"),
            ("referer", "https://kinoplay2.site/"),
            ("sec-ch-ua", "\"Google Chrome\";v=\"129\", \"Not = A ? Brand\";v=\"8\", \"Chromium\";v=\"129\""),
            ("sec-ch-ua-mobile", "?0"),
            ("sec-ch-ua-platform", "\"Windows\""),
            ("sec-fetch-dest", "empty"),
            ("sec-fetch-mode", "cors"),
            ("sec-fetch-site", "cross-site")
        );

        [JSInvokable("lite/videodb")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.VideoDB.Clone();

            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int sid = int.Parse(parse_arg("sid", args) ?? "-1");
            string? t = parse_arg("t", args);

            var oninvk = new VideoDBInvoke
            (
               null,
               init.corsHost(),
               (url, head) => JsHttpClient.Get(init.cors(url), httpHeaders(args, init, baseheader)),
               streamfile => HostStreamProxy(init, streamfile)
            );

            string memkey = $"videodb:view:{arg.kinopoisk_id}";
            refresh: var content = await InvokeCache(arg.id, memkey, () => oninvk.Embed(arg.kinopoisk_id));

            string html = oninvk.Html(content, arg.account_email, arg.kinopoisk_id, arg.title, arg.original_title, t, s, sid, false, true);
            if (string.IsNullOrEmpty(html))
            {
                IMemoryCache.Remove(memkey);
                if (IsRefresh(init, NotUseDefaultApn: true))
                    goto refresh;
            }

            return html;
        }

        [JSInvokable("lite/videodb/manifest")]
        async public static ValueTask<string> Manifest(string args)
        {
            var init = AppInit.VideoDB;
            string? link = parse_arg("link", args);
            if (link == null)
                return string.Empty;

            string? result = await AppInit.JSRuntime.InvokeAsync<string?>("httpReq", init.cors(link), false, new 
            { 
                dataType = "text", timeout = 8 * 1000, 
                headers = JsHttpClient.httpReqHeaders(httpHeaders(args, init, baseheader)),
                returnHeaders = true
            });

            var json = JsonDocument.Parse(result);
            string currentUrl = json.RootElement.GetProperty("currentUrl").GetString();

            AppInit.log("currentUrl: " + currentUrl);

            return "{\"method\":\"play\",\"url\":\"" + HostStreamProxy(init, currentUrl) + "\",\"title\":\"auto\"}";
        }
    }
}
