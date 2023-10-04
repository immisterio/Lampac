using JinEnergy.Engine;
using Microsoft.JSInterop;
using System.Text;
using System.Text.RegularExpressions;

namespace JinEnergy.Online
{
    public class BazonController : BaseController
    {
        [JSInvokable("lite/bazon")]
        async public static ValueTask<string> Index(string args)
        {
            var arg = defaultArgs(args);

            var files = await InvokeCache(arg.id, $"bazon:{arg.kinopoisk_id}", () => Embed(arg.kinopoisk_id));
            if (files == null)
                return OnError("files");

            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            foreach (var f in files)
            {
                string streansquality = "\"quality\": {" + string.Join(",", f.Value.Select(s => $"\"{Regex.Match(s, "=([0-9]+)\\.mp4").Groups[1].Value}p\":\"{s}\"")) + "}";

                html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + f.Value[0] + "\",\"title\":\"" + (arg.title ?? arg.original_title) + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + f.Key + "</div></div>");
                firstjson = false;
            }

            return html.ToString() + "</div>";
        }


        async static ValueTask<Dictionary<string, List<string>>?> Embed(long kinopoisk_id)
        {
            var root = await JsHttpClient.Get<RootObject>($"http://fork.tutkino.fun/4k/4k/?ch=mov&cx={kinopoisk_id}&box_mac=973548692667", timeoutSeconds: 5);
            if (root?.channels == null || root.channels.Count <= 1)
                return null;

            var strems = new Dictionary<string, List<string>>();

            foreach (var c in root.channels)
            {
                string title = Regex.Replace(c.title, "^[0-9]+\\. ", "").Trim();
                if (!strems.ContainsKey(title))
                    strems.Add(title, new List<string>());

                var data = strems[title];
                data.Insert(0, c.stream_url);

                strems[title] = data;
            }

            return strems;
        }


        public class RootObject
        {
            public List<Сhannel> channels { get; set; }
        }

        public class Сhannel
        {
            public string title { get; set; }

            public string stream_url { get; set; }
        }
    }
}
