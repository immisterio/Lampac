using JinEnergy.Engine;
using Microsoft.JSInterop;

namespace JinEnergy.Online
{
    public class BazonController : BaseController
    {
        [JSInvokable("lite/bazon")]
        async public static ValueTask<string> Index(string args)
        {
            var arg = defaultArgs(args);
            int tid = int.Parse(parse_arg("tid", args) ?? "-1");
            int sid = int.Parse(parse_arg("sid", args) ?? "-1");

            if (arg.kinopoisk_id == 0)
                return OnError("arg");

            string uri = $"?kinopoisk_id={arg.kinopoisk_id}";

            if (tid != -1)
                uri += $"&tid={tid}";

            if (sid != -1)
                uri += $"&sid={sid}";

            string? content = await JsHttpClient.Get("https://cors.bwa.workers.dev/bazon.app/" + uri);
            if (string.IsNullOrEmpty(content))
                return OnError("content");

            return content;
        }
    }
}
