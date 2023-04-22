using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Text.RegularExpressions;
using System.Web;

namespace JinEnergy.Engine
{
    public class BaseController : ComponentBase
    {
        public static string OnError(string msg)
        {
            AppInit.JSRuntime?.InvokeVoidAsync("console.log", "BWA", msg);
            return string.Empty;
        }

        public static string? arg(string name, string args)
        {
            string val = Regex.Match(args ?? "", $"(^|&|\\?){name}=([^&]+)").Groups[2].Value;
            if (string.IsNullOrWhiteSpace(val))
                return null;

            return HttpUtility.UrlDecode(val);
        }

        public static void defaultOnlineArgs(string args, out long id, out string? imdb_id, out long kinopoisk_id, out string? title, out string? original_title, out int serial, out string? original_language, out int year, out string? source, out int clarification, out long cub_id, out string? account_email)
        {
            id = long.Parse(arg("id", args) ?? "0");
            imdb_id = arg("imdb_id", args);
            kinopoisk_id = long.Parse(arg("kinopoisk_id", args) ?? "0");
            title = arg("title", args);
            original_title = arg("original_title", args);
            serial = int.Parse(arg("serial", args) ?? "0");
            original_language = arg("original_language", args);
            year = int.Parse(arg("year", args) ?? "0");
            source = arg("source", args);
            clarification = int.Parse(arg("clarification", args) ?? "0");
            cub_id = long.Parse(arg("cub_id", args) ?? "0");
            account_email = arg("account_email", args);
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
