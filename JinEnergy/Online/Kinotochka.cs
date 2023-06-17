using JinEnergy.Engine;
using Microsoft.JSInterop;
using System.Text.RegularExpressions;

namespace JinEnergy.Online
{
    public class KinotochkaController : BaseController
    {
        [JSInvokable("lite/kinotochka")]
        async public static ValueTask<string> Index(string args)
        {
            var arg = defaultArgs(args);

            if (arg.kinopoisk_id == 0)
                return OnError("kinopoisk_id");

            string? file = await InvokeCache(arg.id, $"kinotochka:{arg.kinopoisk_id}", () => Embed(arg.kinopoisk_id));
            if (string.IsNullOrEmpty(file))
                return OnError("file");

            foreach (string f in file.Split(",").Reverse())
            {
                if (string.IsNullOrEmpty(f))
                    continue;

                return "<div class=\"videos__line\"><div class=\"videos__item videos__movie selector focused\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + f + "\",\"title\":\"" + arg.title + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">По умолчанию</div></div></div>";
            }

            return OnError("play_url");
        }


        async static ValueTask<string?> Embed(long kinopoisk_id)
        {
            string? embed = await JsHttpClient.Get($"{AppInit.Kinotochka.corsHost()}/embed/kinopoisk/{kinopoisk_id}", timeoutSeconds: 8);
            string file = Regex.Match(embed ?? "", "id:\"playerjshd\", file:\"(https?://[^\"]+)\"").Groups[1].Value;

            if (string.IsNullOrEmpty(file))
                return null;

            return file;
        }
    }
}
