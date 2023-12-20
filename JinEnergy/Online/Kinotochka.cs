using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Model.Templates;
using System.Text.RegularExpressions;

namespace JinEnergy.Online
{
    public class KinotochkaController : BaseController
    {
        [JSInvokable("lite/kinotochka")]
        async public static ValueTask<string> Index(string args)
        {
            var arg = defaultArgs(args);

            string? file = await InvokeCache(arg.id, $"kinotochka:{arg.kinopoisk_id}", () => Embed(arg.kinopoisk_id));
            if (string.IsNullOrEmpty(file))
                return EmptyError("file");

            var mtpl = new MovieTpl(arg.title, arg.original_title);

            foreach (string f in file.Split(",").Reverse())
            {
                if (string.IsNullOrEmpty(f))
                    continue;

                return mtpl.ToHtml("По умолчанию", HostStreamProxy(AppInit.Kinotochka, f));
            }

            return EmptyError("play_url");
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
