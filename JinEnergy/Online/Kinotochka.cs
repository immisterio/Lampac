using JinEnergy.Engine;
using Lampac.Models.LITE;
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
            var init = AppInit.Kinotochka.Clone();

            refresh: string file = await Embed(args, init, arg.kinopoisk_id);

            var mtpl = new MovieTpl(arg.title, arg.original_title);

            foreach (string f in file.Split(",").Reverse())
            {
                if (string.IsNullOrEmpty(f))
                    continue;

                return mtpl.ToHtml("По умолчанию", HostStreamProxy(init, f));
            }

            if (IsRefresh(init))
                goto refresh;

            return EmptyError("play_url");
        }


        async static ValueTask<string> Embed(string args, OnlinesSettings init, long kinopoisk_id)
        {
            string? embed = await JsHttpClient.Get($"{init.corsHost()}/embed/kinopoisk/{kinopoisk_id}", httpHeaders(args, init));
            return Regex.Match(embed ?? "", "id:\"playerjshd\", file:\"(https?://[^\"]+)\"").Groups[1].Value;
        }
    }
}
