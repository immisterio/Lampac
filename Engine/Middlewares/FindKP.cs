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
            if (httpContext.Request.QueryString.Value.Contains("&source=tmdb") && httpContext.Request.Path.Value.StartsWith("/lite/events"))
            {
                if (!httpContext.Request.QueryString.Value.Contains("&imdb_id="))
                {
                    string tmdb_id = Regex.Match(httpContext.Request.QueryString.Value, "(\\?|&)id=([0-9]+)").Groups[2].Value;
                    if (!string.IsNullOrWhiteSpace(tmdb_id))
                    {
                        string memkey = $"Middlewares:FindKP:{tmdb_id}";
                        if (!mem.TryGetValue(memkey, out string imdb_id))
                        {
                            string cat = httpContext.Request.QueryString.Value.Contains("&serial=1") ? "tv" : "movie";
                            string json = await HttpClient.Get($"https://api.themoviedb.org/3/{cat}/{tmdb_id}?api_key=4ef0d7355d9ffb5151e987764708ce96&append_to_response=external_ids", timeoutSeconds: 5);
                            if (!string.IsNullOrWhiteSpace(json))
                            {
                                imdb_id = Regex.Match(json, "\"imdb_id\":\"(tt[0-9]+)\"").Groups[1].Value;
                                if (!string.IsNullOrWhiteSpace(imdb_id))
                                    mem.Set(memkey, imdb_id);
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(imdb_id))
                        {
                            httpContext.Response.Redirect(httpContext.Request.Path.Value + httpContext.Request.QueryString.Value + $"&imdb_id={imdb_id}");
                            return;
                        }
                    }
                }
                else if (!httpContext.Request.QueryString.Value.Contains("&kinopoisk_id="))
                {
                    string imdb = Regex.Match(httpContext.Request.QueryString.Value, "(\\?|&)imdb_id=([^&]+)").Groups[2].Value;
                    if (!string.IsNullOrWhiteSpace(imdb))
                    {
                        string memkey = $"Middlewares:FindKP:{imdb}";
                        if (!mem.TryGetValue(memkey, out string kpid))
                        {
                            switch (AppInit.conf.online.findkp ?? "alloha")
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

                            if (!string.IsNullOrWhiteSpace(kpid) && kpid != "0" && kpid != "null")
                                mem.Set(memkey, kpid);
                        }

                        if (!string.IsNullOrWhiteSpace(kpid) && kpid != "0" && kpid != "null")
                        {
                            httpContext.Response.Redirect(httpContext.Request.Path.Value + httpContext.Request.QueryString.Value + $"&kinopoisk_id={kpid}");
                            return;
                        }
                    }
                }
            }

            await _next(httpContext);
        }

        async ValueTask<string> getAlloha(string imdb)
        {
            string json = await HttpClient.Get("https://api.alloha.tv/?token=04941a9a3ca3ac16e2b4327347bbc1&imdb=" + imdb, timeoutSeconds: 5);
            if (json == null)
                return null;

            return Regex.Match(json, "\"id_kp\":([0-9]+),").Groups[1].Value;
        }

        async ValueTask<string> getVSDN(string imdb)
        {
            string json = await HttpClient.Get("http://cdn.svetacdn.in/api/short?api_token=3i40G5TSECmLF77oAqnEgbx61ZWaOYaE&imdb_id=" + imdb, timeoutSeconds: 5);
            if (json == null)
                return null;

            return Regex.Match(json, "\"kp_id\":\"([0-9]+)\"").Groups[1].Value;
        }

        async ValueTask<string> getTabus(string imdb)
        {
            string json = await HttpClient.Get("https://api.bhcesh.me/list?token=eedefb541aeba871dcfc756e6b31c02e&imdb_id=" + imdb, timeoutSeconds: 5);
            if (json == null)
                return null;

            return Regex.Match(json, "\"kinopoisk_id\":\"([0-9]+)\"").Groups[1].Value;
        }
    }
}
