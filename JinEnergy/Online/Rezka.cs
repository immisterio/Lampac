using JinEnergy.Engine;
using Lampac.Models.LITE;
using Microsoft.JSInterop;
using Shared.Engine.Online;
using Shared.Model.Online;
using System.Text.RegularExpressions;

namespace JinEnergy.Online
{
    public class RezkaController : BaseController
    {
        #region RezkaInvoke
        static bool origstream;

        static RezkaInvoke rezkaInvoke(string args, RezkaSettings init)
        {
            string rhsHost = init.corsHost();
            var headers = httpHeaders(args, init);

            string fixCookie(string cook)
            {
                return $"dle_user_taken=1; {Regex.Match(cook, "(dle_user_id=[^;]+;)")} {Regex.Match(cook, "(dle_password=[^;]+)")}".Trim();
            }

            bool ispremium = false;
            if (init.premium && !string.IsNullOrEmpty(init.cookie))
            {
                ispremium = true;
                rhsHost = "kwwsv=22odps1df";
                headers = httpHeaders(args, init, HeadersModel.Init(
                   ("X-Lampac-App", "1"),
                   ("X-Lampac-Version", "bwajs"),
                   ("X-Lampac-Device-Id", AppInit.KitUid),
                   ("X-Lampac-Cookie", fixCookie(init.cookie))
                ));
            }
            else if (!string.IsNullOrEmpty(init.cookie))
                headers.Add(new HeadersModel("cookie", fixCookie(init.cookie)));

            //bool userapn = IsApnIncluded(init);

            return new RezkaInvoke
            (
                null,
                rhsHost,
                init.scheme,
                MaybeInHls(init.hls, init),
                !string.IsNullOrEmpty(init.cookie),
                ongettourl => JsHttpClient.Get(init.cors(ongettourl), headers),
                (url, data) => JsHttpClient.Post(init.cors(url), data, headers),
                streamfile => HostStreamProxy(init, ispremium ? streamfile : RezkaInvoke.fixcdn(init.forceua ? "UA" : AppInit.Country, init.uacdn, streamfile)),
                AppInit.log
                //streamfile => userapn ? HostStreamProxy(init, streamfile) : DefaultStreamProxy(streamfile, origstream)
            );
        }
        #endregion

        [JSInvokable("lite/rezka")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.Rezka.Clone();
            var oninvk = rezkaInvoke(args, init);

            if (AppInit.typeConf == "web" && !init.premium)
                return ShowError("Нужен HDRezka Premium");

            if (AppInit.Country == "RU")
            {
                if (string.IsNullOrEmpty(init.cookie))
                    return ShowError("Авторизуйтесь на https://bwa.to/bind/rezka");

                else if (!AppInit.IsAndrod)
                    return ShowError("Нужен HDRezka Premium");
            }

            var arg = defaultArgs(args);
            string? t = parse_arg("t", args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            string? href = parse_arg("href", args);

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(arg.title) || arg.year == 0))
                return ShowError("Отсутствуют параметры href/title");

            string memkey = $"rezka:{arg.kinopoisk_id}:{arg.imdb_id}:{arg.title}:{arg.original_title}:{arg.year}:{arg.clarification}:{href}";
            refresh: var content = await InvokeCache(arg.id, memkey, () => oninvk.Embed(arg.kinopoisk_id, arg.imdb_id, arg.title, arg.original_title, arg.clarification, arg.year, href));

            string html = oninvk.Html(content, arg.account_email, arg.kinopoisk_id, arg.imdb_id, arg.title, arg.original_title, arg.clarification, arg.year, s, href, false);
            if (string.IsNullOrEmpty(html))
            {
                IMemoryCache.Remove(memkey);
                if (IsRefresh(init, NotUseDefaultApn: true))
                    goto refresh;

                return EmptyError("Не удалось получить html");
            }

            return html;
        }


        #region Serial
        [JSInvokable("lite/rezka/serial")]
        async public static ValueTask<string> Serial(string args)
        {
            var init = AppInit.Rezka.Clone();
            var oninvk = rezkaInvoke(args, init);

            var arg = defaultArgs(args);
            int t = int.Parse(parse_arg("t", args) ?? "0");
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            string? href = parse_arg("href", args);

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(arg.title) || arg.year == 0))
                return EmptyError("arg");

            refresh: var root = await InvokeCache(0, $"rezka:serial:{arg.id}:{t}", () => oninvk.SerialEmbed(arg.id, t));
            var content = await InvokeCache(0, $"rezka:{arg.kinopoisk_id}:{arg.imdb_id}:{arg.title}:{arg.original_title}:{arg.year}:{arg.clarification}:{href}", () => oninvk.Embed(arg.kinopoisk_id, arg.imdb_id, arg.title, arg.original_title, arg.clarification, arg.year, href));

            string html = oninvk.Serial(root, content, arg.account_email, arg.kinopoisk_id, arg.imdb_id, arg.title, arg.original_title, arg.clarification, arg.year, href, arg.id, t, s, false);
            if (string.IsNullOrEmpty(html))
            {
                IMemoryCache.RemoveAll("rezka:serial");
                if (IsRefresh(init, NotUseDefaultApn: true))
                    goto refresh;
            }

            return html;
        }
        #endregion

        #region Movie
        [JSInvokable("lite/rezka/movie")]
        async public static ValueTask<string> Movie(string args)
        {
            var init = AppInit.Rezka.Clone();
            var oninvk = rezkaInvoke(args, init);

            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int e = int.Parse(parse_arg("e", args) ?? "-1");
            int t = int.Parse(parse_arg("t", args) ?? "0");
            int director = int.Parse(parse_arg("director", args) ?? "0");

            string memkey = $"rezka:movie:get_cdn_series:{arg.id}:{t}:{director}:{s}:{e}";
            refresh: var md = await InvokeCache(0, memkey, () => oninvk.Movie(arg.id, t, director, s, e, parse_arg("favs", args)));
            if (md == null)
            {
                IMemoryCache.Remove(memkey);

                if (IsRefresh(init, NotUseDefaultApn: true))
                    goto refresh;

                return EmptyError("md");
            }

            //if (!IsApnIncluded(AppInit.Rezka))
            //    origstream = await IsOrigStream(md.links[0].stream_url!);

            return oninvk.Movie(md, arg.title, arg.original_title, false);
        }
        #endregion
    }
}
