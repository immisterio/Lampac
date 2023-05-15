using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Text.RegularExpressions;
using System.Web;

namespace JinEnergy.Engine
{
    public class BaseController : ComponentBase
    {
        public static IJSRuntime? JSRuntime => AppInit.JSRuntime;

        public static string OnError(string msg)
        {
            AppInit.JSRuntime?.InvokeVoidAsync("console.log", "BWA", msg);
            return string.Empty;
        }

        public static string? parse_arg(string name, string args)
        {
            string val = Regex.Match(args ?? "", $"(^|&|\\?){name}=([^&]+)").Groups[2].Value;
            if (string.IsNullOrWhiteSpace(val))
                return null;

            return HttpUtility.UrlDecode(val);
        }

        public static (long id, string? imdb_id, long kinopoisk_id, string? title, string? original_title, int serial, string? original_language, int year, string? source, int clarification, long cub_id, string? account_email) 
            defaultArgs(string args)
        {
            return
            (
               long.Parse(parse_arg("id", args) ?? "0"),
               parse_arg("imdb_id", args),
               long.Parse(parse_arg("kinopoisk_id", args) ?? "0"),
               parse_arg("title", args),
               parse_arg("original_title", args),
               int.Parse(parse_arg("serial", args) ?? "0"),
               parse_arg("original_language", args),
               int.Parse(parse_arg("year", args) ?? "0"),
               parse_arg("source", args),
               int.Parse(parse_arg("clarification", args) ?? "0"),
               long.Parse(parse_arg("cub_id", args) ?? "0"),
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
    }
}
