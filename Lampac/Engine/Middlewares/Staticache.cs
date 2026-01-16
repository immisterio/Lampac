using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Shared;
using Shared.Engine;
using Shared.Models;
using Shared.Models.AppConf;
using Shared.Models.Events;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class Staticache
    {
        #region Staticache
        protected static readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        static readonly ConcurrentDictionary<string, (DateTime ex, string contentType)> cacheFiles = new();

        static readonly Timer cleanupTimer = new Timer(cleanup, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        static Staticache()
        {
            Directory.CreateDirectory("cache/static");

            var now = DateTime.Now;

            foreach (string inFile in Directory.EnumerateFiles("cache/static", "*"))
            {
                string fileName = Path.GetFileName(inFile);
                if (!fileName.Contains("-") || !fileName.Contains("."))
                {
                    File.Delete(inFile);
                    continue;
                }

                string[] tmp = fileName.Split('-');
                string cachekey = tmp[0];

                tmp = tmp[1].Split(".");
                string contentType = tmp[1] == "html" ? "text/html; charset=utf-8" : "application/json; charset=utf-8";

                if (!long.TryParse(tmp[0], out long _ex) || _ex == 0)
                {
                    File.Delete(inFile);
                    continue;
                }

                var ex = DateTime.FromFileTime(_ex);
                if (now > ex)
                {
                    File.Delete(inFile);
                    continue;
                }

                cacheFiles.TryAdd(cachekey, (ex, contentType));
            }
        }

        static void cleanup(object state)
        {
            try
            {
                var now = DateTime.Now;

                foreach (var _c in cacheFiles)
                {
                    try
                    {
                        if (_c.Value.ex > now)
                            continue;

                        string cachefile = getFilePath(_c.Key, _c.Value.ex, _c.Value.contentType);
                        File.Delete(cachefile);

                        cacheFiles.TryRemove(_c.Key, out _);
                    }
                    catch { }
                }
            }
            catch { }
        }


        private readonly RequestDelegate _next;

        public Staticache(RequestDelegate next)
        {
            _next = next;
        }
        #endregion

        async public Task InvokeAsync(HttpContext httpContext)
        {
            var init = AppInit.conf.Staticache;

            if (init.enable != true || init.routes.Count == 0)
            {
                await _next(httpContext);
                return;
            }

            if (InvkEvent.IsStaticache())
            {
                var requestInfo = httpContext.Features.Get<RequestModel>();

                bool next = InvkEvent.Staticache(new EventStaticache(httpContext, requestInfo));
                if (!next)
                {
                    await _next(httpContext);
                    return;
                }
            }

            StaticacheRoute route = null;

            foreach (var r in init.routes)
            {
                if (Regex.IsMatch(httpContext.Request.Path.Value, r.pathRex))
                {
                    route = r;
                    break;
                }
            }

            if (route == null)
            {
                await _next(httpContext);
                return;
            }

            string cachekey = getQueryKeys(httpContext, route.queryKeys);

            if (cacheFiles.TryGetValue(cachekey, out var _r) && _r.ex > DateTime.Now)
            {
                httpContext.Response.Headers["X-StatiCache-Status"] = "HIT";
                httpContext.Response.ContentType = _r.contentType ?? route.contentType ?? "text/html; charset=utf-8";

                string cachefile = getFilePath(cachekey, _r.ex, httpContext.Response.ContentType);
                await httpContext.Response.SendFileAsync(cachefile, httpContext.RequestAborted).ConfigureAwait(false);
                return;
            }

            using (var msm = PoolInvk.msm.GetStream())
            {
                httpContext.Features.Set(msm);
                httpContext.Response.Headers["X-StatiCache-Status"] = "MISS";

                await _next(httpContext);

                if (msm.Length > 0)
                {
                    try
                    {
                        await semaphore.WaitAsync();

                        var ex = DateTime.Now.AddMinutes(route.cacheMinutes);
                        string cachefile = getFilePath(cachekey, ex, route.contentType);

                        msm.Position = 0;
                        using (var fileStream = File.OpenWrite(cachefile))
                            await msm.CopyToAsync(fileStream, PoolInvk.bufferSize);

                        cacheFiles.AddOrUpdate(cachekey, (ex, route.contentType), (k,v) => (ex, route.contentType));
                    }
                    catch (Exception ex) 
                    { 
                        Console.WriteLine($"Staticache, route: {route.pathRex}\n{ex.Message}\n\n"); 
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }
            }
        }


        static string getQueryKeys(HttpContext httpContext, string[] keys)
        {
            if (keys == null || keys.Length == 0)
                return string.Empty;

            var sb = new StringBuilder();

            foreach (string key in keys)
            {
                if (httpContext.Request.Query.TryGetValue(key, out StringValues value))
                {
                    sb.Append(key);
                    sb.Append(":");
                    sb.Append(value.ToString());
                    sb.Append(":");
                }
            }

            if (httpContext.Request.Query.TryGetValue("rjson", out StringValues rjson))
            {
                sb.Append("rjson:");
                sb.Append(rjson.ToString());
            }

            return CrypTo.md5(sb.ToString());
        }

        static string getFilePath(string cachekey, DateTime ex, string contentType)
        {
            return $"cache/static/{cachekey}-{ex.ToFileTime()}.{(contentType?.Contains("text/html") == true ? "html" : "json")}";
        }
    }
}
