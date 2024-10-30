using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;
using Shared.Model.Online;

namespace JinEnergy.Online
{
    public class KinoukrController : BaseController
    {
        [JSInvokable("lite/kinoukr")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.Kinoukr.Clone();

            var arg = defaultArgs(args);
            string? href = parse_arg("href", args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int t = int.Parse(parse_arg("t", args) ?? "-1");
            
            var oninvk = new KinoukrInvoke
            (
               null,
               init.corsHost(),
               ongettourl => JsHttpClient.Get(init.cors(ongettourl), httpHeaders(args, init)),
               (url, data) => JsHttpClient.Post(init.cors(url), data, url.Contains("bobr-kurwa") ? httpHeaders(args, init) : httpHeaders(args, init, HeadersModel.Init
               (
                    ("cache-control", "no-cache"),
                    ("cookie", $"PHPSESSID={KinoukrInvoke.unic(32)}; legit_user=1;"),
                    ("dnt", "1"),
                    ("origin", init.host),
                    ("pragma", "no-cache"),
                    ("priority", "u=0, i"),
                    ("referer", $"{init.host}/{KinoukrInvoke.unic(4, true)}-{KinoukrInvoke.unic(Random.Shared.Next(4, 8))}-{KinoukrInvoke.unic(Random.Shared.Next(5, 10))}.html"),
                    ("sec-ch-ua", "\"Chromium\";v=\"130\", \"Google Chrome\";v=\"130\", \"Not ? A_Brand\";v=\"99\""),
                    ("sec-ch-ua-arch", "\"x86\""),
                    ("sec-ch-ua-bitness", "\"64\""),
                    ("sec-ch-ua-full-version", "\"130.0.6723.70\""),
                    ("sec-ch-ua-full-version-list", "\"Chromium\";v=\"130.0.6723.70\", \"Google Chrome\";v=\"130.0.6723.70\", \"Not ? A_Brand\";v=\"99.0.0.0\""),
                    ("sec-ch-ua-mobile", "?0"),
                    ("sec-ch-ua-model", "\"\""),
                    ("sec-ch-ua-platform", "\"Windows\""),
                    ("sec-ch-ua-platform-version", "\"10.0.0\""),
                    ("sec-fetch-dest", "document"),
                    ("sec-fetch-mode", "navigate"),
                    ("sec-fetch-site", "same-origin"),
                    ("sec-fetch-user", "?1"),
                    ("upgrade-insecure-requests", "1")
               ))),
               streamfile => HostStreamProxy(init, streamfile)
               //AppInit.log
            );

            string memkey = string.IsNullOrEmpty(href) ? $"kinoukr:{arg.original_title}:{arg.year}:{arg.clarification}" : $"kinoukr:{href}";
            refresh: var content = await InvokeCache(arg.id, memkey, () => oninvk.EmbedKurwa(arg.clarification == 1 ? arg.title : arg.original_title, arg.year));

            string html = oninvk.Html(content, arg.clarification, arg.title, arg.original_title, arg.year, t, s, href);
            if (string.IsNullOrEmpty(html))
            {
                IMemoryCache.Remove(memkey);
                if (IsRefresh(init, true))
                    goto refresh;
            }
            
            return html;
        }
    }
}
