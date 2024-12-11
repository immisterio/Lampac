using JinEnergy.Engine;
using Lampac.Models.LITE;
using Microsoft.JSInterop;
using Shared.Engine.Online;
using Shared.Model.Online;
using Shared.Model.Online.Lumex;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace JinEnergy.Online
{
    public class DurexLabController : BaseController
    {
        static string domain = Encoding.UTF8.GetString(Convert.FromBase64String("bW92aWVsYWIub25l"));
        static LumexSettings init = new LumexSettings(null, null, "oxph{1sz", Encoding.UTF8.GetString(Convert.FromBase64String("Q1dmS1hMYzFhaklk")));

        [JSInvokable("lite/durexlab")]
        async public static ValueTask<string> Index(string args)
        {
            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            string? t = parse_arg("t", args);

            var oninvk = new LumexInvoke
            (
               init,
               (url, head) => JsHttpClient.Get(url),
               streamfile => streamfile
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
                var body = JsonDocument.Parse(json.RootElement.GetProperty("body").GetString());

                AppInit.log("???");
                AppInit.log("player2 " + body.RootElement.GetProperty("player").GetRawText());

                var md = body.RootElement.GetProperty("player").Deserialize<EmbedModel>();
                if (md == null)
                    return null;

                md.csrf = Regex.Match(json.RootElement.GetProperty("headers").GetProperty("set-cookie").GetRawText(), "x-csrf-token=([^\n\r; ]+)").Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(md.csrf))
                    return null;

                return md;
            });

            return oninvk.Html(content, string.Empty, arg.imdb_id, arg.kinopoisk_id, arg.title, arg.original_title, t, s, bwa: true);
        }


        [JSInvokable("lite/lumex/video")]
        async public static ValueTask<string> Video(string args)
        {
            string? playlist = parse_arg("playlist", args);
            string? csrf = parse_arg("csrf", args)?.Replace("|", "%7C");
            if (playlist == null || csrf == null)
                return string.Empty;

            var result = await JsHttpClient.Post<JsonNode>($"https://api.{init.iframehost}" + playlist, "{}", useDefaultHeaders: false, addHeaders: HeadersModel.Init(
                ("accept", "*/*"),
                ("cache-control", "no-cache"),
                ("cookie", $"x-csrf-token={csrf}"),
                ("dnt", "1"),
                ("Origin", $"https://p.{init.iframehost}"),
                ("Referer", $"https://p.{init.iframehost}/"),
                ("pragma", "no-cache"),
                ("priority", "u=1, i"),
                ("Sec-Ch-Ua", "\"Google Chrome\";v=\"131\", \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "same-site"),
                ("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"),
                ("x-csrf-token", csrf.Split("%")[0])
            ));

            string? url = result?["url"]?.ToString();
            if (string.IsNullOrEmpty(url))
                return string.Empty;

            return "{\"method\":\"play\",\"url\":\"" + HostStreamProxy(init, $"http:{url}") + "\",\"title\":\"1080p\"}";
        }
    }
}
