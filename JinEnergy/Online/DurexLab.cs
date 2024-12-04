using JinEnergy.Engine;
using Lampac.Models.LITE;
using Microsoft.JSInterop;
using Shared.Engine.Online;
using Shared.Model.Online;
using Shared.Model.Online.Lumex;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JinEnergy.Online
{
    public class DurexLabController : BaseController
    {
        [JSInvokable("lite/durexlab")]
        async public static ValueTask<string> Index(string args)
        {
            var init = new LumexSettings(null, null, "oxph{1sz", Encoding.UTF8.GetString(Convert.FromBase64String("Q1dmS1hMYzFhaklk")));
            string domain = Encoding.UTF8.GetString(Convert.FromBase64String("bW92aWVsYWIub25l"));

            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            string? t = parse_arg("t", args);

            var oninvk = new LumexInvoke
            (
               init,
               (url, head) => JsHttpClient.Get(url),
               streamfile => HostStreamProxy(init, streamfile)
            );

            string memkey = $"durexlab:view:{arg.kinopoisk_id}";
            var content = await InvokeCache(arg.id, memkey, async () => 
            {
                string? result = await AppInit.JSRuntime.InvokeAsync<string?>("httpReq", $"https://api.{init.iframehost}/content?clientId={init.clientId}&contentType=short&kpId={arg.kinopoisk_id}&domain={domain}&url={domain}", false, new
                {
                    dataType = "text",
                    timeout = 8 * 1000,
                    headers = JsHttpClient.httpReqHeaders(HeadersModel.Init(
                        ("Accept", "*/*"),
                        ("Origin", $"https://p.{init.iframehost}"),
                        ("Referer", $"https://p.{init.iframehost}/"),
                        ("Sec-Ch-Ua", "\"Google Chrome\";v=\"131\", \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\""),
                        ("Sec-Ch-Ua-Mobile", "?0"),
                        ("Sec-Ch-Ua-Platform", "\"Windows\""),
                        ("Sec-Fetch-Dest", "empty"),
                        ("Sec-Fetch-Mode", "cors"),
                        ("Sec-Fetch-Site", "same-site"),
                        ("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36")
                    )),
                    returnHeaders = true
                });

                if (string.IsNullOrEmpty(result))
                    return null;

                var json = JsonDocument.Parse(result);

                AppInit.log("cookie - " + json.RootElement.GetProperty("headers").GetProperty("set-cookie").GetRawText());

                var md = json.RootElement.GetProperty("body").Deserialize<EmbedModel>();
                if (md == null)
                    return null;

                md.csrf = Regex.Match(json.RootElement.GetProperty("headers").GetProperty("set-cookie").GetRawText(), "x-csrf-token=([^\n\r; ]+)").Groups[1].Value.Trim();

                AppInit.log("csrf - " + md.csrf);

                if (string.IsNullOrEmpty(md.csrf))
                    return null;

                return md;
            });

            return oninvk.Html(content, string.Empty, arg.imdb_id, arg.kinopoisk_id, arg.title, arg.original_title, t, s);
        }


        [JSInvokable("lite/lumex/video.m3u8")]
        async public static ValueTask<string> Video(string args)
        {
            var init = AppInit.VideoDB;
            string? link = parse_arg("link", args);
            if (link == null)
                return string.Empty;

            string? result = await AppInit.JSRuntime.InvokeAsync<string?>("httpReq", init.cors(link), false, new 
            { 
                dataType = "text", timeout = 8 * 1000, 
                headers = JsHttpClient.httpReqHeaders(httpHeaders(args, init)),
                returnHeaders = true
            });

            var json = JsonDocument.Parse(result);
            string currentUrl = json.RootElement.GetProperty("currentUrl").GetString();

            return "{\"method\":\"play\",\"url\":\"" + HostStreamProxy(init, currentUrl) + "\",\"title\":\"1080p\"}";
        }
    }
}
