using JinEnergy.Model;
using Lampac.Models.SISI;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Shared.Model.Base;
using Shared.Model.Online;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;

namespace JinEnergy.Engine
{
    public class BaseController : ComponentBase
    {
        public static IJSRuntime? JSRuntime => AppInit.JSRuntime;

        public static ResultModel OnError() => OnError(string.Empty);

        public static ResultModel OnError(string msg)
        {
            if (!string.IsNullOrEmpty(msg) && AppInit.JSRuntime != null)
                AppInit.JSRuntime.InvokeVoidAsync("console.log", "BWA", msg).ConfigureAwait(false);

            return new ResultModel() { error = "html" };
        }

        public static string EmptyError(string msg)
        {
            if (!string.IsNullOrEmpty(msg) && AppInit.JSRuntime != null)
                AppInit.JSRuntime.InvokeVoidAsync("console.log", "BWA", msg).ConfigureAwait(false);

            return string.Empty;
        }

        public static string? parse_arg(string name, string args)
        {
            if (args == null || !args.Contains($"{name}="))
                return null;

            string val = Regex.Match(args ?? "", $"(^|&|\\?){name}=([^&]+)").Groups[2].Value;
            if (string.IsNullOrEmpty(val))
                return null;

            return HttpUtility.UrlDecode(val);
        }

        public static (long id, string? imdb_id, long kinopoisk_id, string? title, string? original_title, int serial, string? original_language, int year, string? source, int clarification, long cub_id, string? account_email) 
            defaultArgs(string args)
        {
            long longParse(string name)
            {
                string? _val = parse_arg(name, args) ?? "0";
                _val = Regex.Replace(_val, "[^0-9]+", "");
                return long.Parse(_val);
            }

            return
            (
               longParse("id"),
               parse_arg("imdb_id", args),
               longParse("kinopoisk_id"),
               parse_arg("title", args),
               parse_arg("original_title", args),
               int.Parse(parse_arg("serial", args) ?? "0"),
               parse_arg("original_language", args),
               int.Parse(parse_arg("year", args) ?? "0"),
               parse_arg("source", args),
               int.Parse(parse_arg("clarification", args) ?? "0"),
               longParse("cub_id"),
               parse_arg("account_email", args)
            );
        }

        async public static ValueTask<T?> InvokeCache<T>(long id, string memKey, Func<ValueTask<T?>> onresult) where T : class
        {
            var cache = IMemoryCache.Read<T>(id, memKey);
            if (cache != null)
                return cache;

            var val = await onresult.Invoke();
            if (val == null || val.Equals(default(T)))
                return default;

            IMemoryCache.Set(memKey, val);
            return val;
        }

        async public static ValueTask<T> InvStructCache<T>(long id, string memKey, Func<ValueTask<T>> onresult) where T : struct
        {
            var cache = IMemoryCache.Read<T>(id, memKey);
            if (!cache.Equals(default(T)))
                return cache;

            var val = await onresult.Invoke();
            if (val.Equals(default(T)))
                return default;

            IMemoryCache.Set(memKey, val);
            return val;
        }


        public static ResultModel OnResult(List<MenuItem>? menu, List<PlaylistItem>? playlists, int total_pages = 0)
        {
            if (playlists == null || playlists.Count == 0)
                return OnError("playlists");

            return new ResultModel() { menu = menu, total_pages = total_pages, list = playlists };
        }

        public static ResultModel OnResult(Istreamproxy conf, Dictionary<string, string>? stream_links)
        {
            if (stream_links == null || stream_links.Count == 0)
                return OnError("stream_links");

            return OnResult(conf, new StreamItem() { qualitys = stream_links });
        }

        public static ResultModel OnResult(Istreamproxy conf, StreamItem? stream_links, bool isebalovo = false)
        {
            if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                return OnError("stream_links");

            List<PlaylistItem>? recomends = null;
            if (stream_links.recomends != null && stream_links.recomends.Count > 0)
            {
                recomends = new List<PlaylistItem>();
                foreach (var pl in stream_links.recomends)
                {
                    recomends.Add(new PlaylistItem() 
                    {
                        name = pl.name,
                        video = HostStreamProxy(conf, pl.video),
                        picture = isebalovo ? $"https://vi.sisi.am/poster.jpg?href={pl.picture}&r=200" : rsizehost(pl.picture, 100),
                        json = pl.json
                    });
                }
            }

            string? apn = string.IsNullOrEmpty(conf?.apn?.host) ? AppInit.apn?.host : conf?.apn?.host;
            bool isDefaultApn = conf != null && Shared.Model.AppInit.IsDefaultApnOrCors(apn);

            if (IsApnIncluded(conf)/* && (!isDefaultApn || !stream_links.qualitys.First().Value.Contains(".m3u"))*/)
            {
                return new ResultModel()
                {
                    qualitys = stream_links.qualitys.ToDictionary(k => k.Key, v => HostStreamProxy(conf, v.Value)),
                    recomends = recomends
                };
            }

            return new ResultModel()
            {
                qualitys = stream_links.qualitys,
                recomends = recomends,
                qualitys_proxy = /*(isDefaultApn && stream_links.qualitys.First().Value.Contains(".m3u")) ? null :*/ stream_links.qualitys.ToDictionary(k => k.Key, v => $"{apn}/{v.Value}")
            };
        }


        public static string? rsizehost(string? url, int height = 200, int width = 0)
        {
            if (string.IsNullOrEmpty(url))
                return url;

            return "https://image-resizing.sisi.am" + $"/{(width == 0 ? "-" : width)}:{(height == 0 ? "-" : height)}/{Regex.Replace(url, "^https?://", "")}";
        }


        public static bool MaybeInHls(bool hls, BaseSettings init)
        {
            if (!string.IsNullOrEmpty(init.apn?.host) && Shared.Model.AppInit.IsDefaultApnOrCors(init.apn?.host))
                return false;

            if (init.apnstream && Shared.Model.AppInit.IsDefaultApnOrCors(AppInit.apn?.host))
                return false;

            return hls;
        }

        async public static ValueTask<bool> IsOrigStream(string? uri, int timeout = 2)
        {
            if (string.IsNullOrWhiteSpace(uri) || uri.Contains("ukrtelcdn.") || AppInit.Country != "UA")
                return true;

            return await JsHttpClient.StatusCode(uri, timeout) is 200 or 301 or 302 or 0;
        }

        public static string DefaultStreamProxy(string? uri, bool orig = false)
        {
            if (string.IsNullOrWhiteSpace(uri))
                return string.Empty;

            if (string.IsNullOrEmpty(AppInit.apn?.host) || AppInit.Country != "UA" || orig || uri.Contains("ukrtelcdn."))
                return uri;

            return $"{AppInit.apn.host}/{uri}";
        }

        public static string HostStreamProxy(Istreamproxy conf, string? uri)
        {
            string? apn = string.IsNullOrEmpty(conf?.apn?.host) ? AppInit.apn?.host : conf?.apn?.host;
            if (conf == null || string.IsNullOrEmpty(uri) || string.IsNullOrEmpty(apn) || !apn.StartsWith("http"))
                return uri;

            if (conf.streamproxy || conf.apnstream)
                return $"{apn}/{uri}";

            if (conf.geostreamproxy != null && conf.geostreamproxy.Count > 0)
            {
                if (!string.IsNullOrEmpty(AppInit.Country) && conf.geostreamproxy.Contains(AppInit.Country))
                    return $"{apn}/{uri}";
            }

            return uri;
        }

        public static bool IsApnIncluded(Istreamproxy? conf)
        {
            string? apn = string.IsNullOrEmpty(conf?.apn?.host) ? AppInit.apn?.host : conf?.apn?.host;
            if (conf == null || string.IsNullOrEmpty(apn))
                return false;

            if (conf.geostreamproxy != null && conf.geostreamproxy.Count > 0)
            {
                if (!string.IsNullOrEmpty(AppInit.Country) && conf.geostreamproxy.Contains(AppInit.Country))
                    return true;
            }

            return conf.streamproxy;
        }

        public static bool IsRefresh(BaseSettings conf, bool NotUseDefaultApn = false)
        {
            if (NotUseDefaultApn)
            {
                if (Shared.Model.AppInit.IsDefaultApnOrCors(conf.apn?.host ?? AppInit.apn?.host) || Shared.Model.AppInit.IsDefaultApnOrCors(Shared.Model.AppInit.corseuhost))
                    return false;
            }

            if (string.IsNullOrEmpty(Shared.Model.AppInit.corseuhost) && string.IsNullOrEmpty(conf.webcorshost))
                return false;

            if (conf.corseu || !string.IsNullOrEmpty(conf.webcorshost))
                return false;

            return conf.corseu = true;
        }

        public static List<HeadersModel> httpHeaders(string args, BaseSettings init, List<HeadersModel>? _startHeaders = null)
        {
            var headers = HeadersModel.Init(_startHeaders);
            if (init.headers == null)
                return headers;

            string? account_email = parse_arg("account_email", args);

            foreach (var h in init.headers)
            {
                if (string.IsNullOrEmpty(h.val) || string.IsNullOrEmpty(h.name))
                    continue;

                string val = h.name.Contains("{") ? h.name.Replace("{account_email}", account_email) : h.name;

                if (val.Contains("{arg:"))
                {
                    foreach (Match m in Regex.Matches(val, "\\{arg:([^\\}]+)\\}"))
                        val = val.Replace(m.Groups[0].Value, parse_arg(m.Groups[1].Value, args));
                }

                headers.Add(new HeadersModel(h.val, val));
            }

            return headers;
        }
    }
}
