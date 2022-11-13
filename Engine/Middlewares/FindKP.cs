using Lampac.Engine.CORE;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class FindKP
    {
        IMemoryCache mem;
        private readonly RequestDelegate _next;

        public FindKP(RequestDelegate next, IMemoryCache mem)
        {
            _next = next;
            this.mem = mem;
        }

        async public Task InvokeAsync(HttpContext httpContext)
        {
            if (httpContext.Request.QueryString.Value.Contains("&source=tmdb") && !httpContext.Request.QueryString.Value.Contains("&kinopoisk_id="))
            {
                if (Regex.IsMatch(httpContext.Request.Path.Value, "^/lite/(hdvb|bazon|ashdi|zetflix)"))
                {
                    string imdb = Regex.Match(httpContext.Request.QueryString.Value, "(\\?|&)imdb_id=([^&]+)").Groups[2].Value;
                    if (string.IsNullOrWhiteSpace(imdb))
                        return;

                    string memkey = $"Middlewares:FindKP:{imdb}";
                    if (!mem.TryGetValue(memkey, out string kpid))
                    {
                        switch (AppInit.conf.findkp ?? "alloha")
                        {
                            case "alloha":
                                kpid = await getAlloha(imdb);
                                break;
                            case "vsdn":
                                kpid = await getVSDN(imdb);
                                break;
                            case "tabus":
                                kpid = await getTabus(imdb);
                                break;
                        }

                        if (string.IsNullOrWhiteSpace(kpid) || kpid == "0" || kpid == "null")
                            return;

                        mem.Set(memkey, kpid);
                    }

                    httpContext.Response.Redirect(httpContext.Request.Path.Value + httpContext.Request.QueryString.Value + $"&kinopoisk_id={kpid}");
                    return;
                }
            }

            await _next(httpContext);
        }

        async ValueTask<string> getAlloha(string imdb)
        {
            string json = await HttpClient.Get("https://api.alloha.tv/?token=04941a9a3ca3ac16e2b4327347bbc1&imdb=" + imdb, timeoutSeconds: 8);
            if (json == null)
                return null;

            return Regex.Match(json, "\"id_kp\":([0-9]+),").Groups[1].Value;
        }

        async ValueTask<string> getVSDN(string imdb)
        {
            string json = await HttpClient.Get("http://cdn.svetacdn.in/api/short?api_token=3i40G5TSECmLF77oAqnEgbx61ZWaOYaE&imdb_id=" + imdb, timeoutSeconds: 8);
            if (json == null)
                return null;

            return Regex.Match(json, "\"kp_id\":\"([0-9]+)\"").Groups[1].Value;
        }

        async ValueTask<string> getTabus(string imdb)
        {
            string json = await HttpClient.Get("https://api.bhcesh.me/list?token=eedefb541aeba871dcfc756e6b31c02e&imdb_id=" + imdb, timeoutSeconds: 8);
            if (json == null)
                return null;

            return Regex.Match(json, "\"kinopoisk_id\":\"([0-9]+)\"").Groups[1].Value;
        }
    }
}
