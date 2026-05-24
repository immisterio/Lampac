using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Primitives;
using Shared;
using Shared.Attributes;
using Shared.Models.AppConf;
using Shared.Models.Base;
using Shared.Models.Events;
using Shared.Services.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Middlewares;

public readonly record struct CacheModel(DateTimeOffset ex, string ext);

public class Staticache
{
    #region static
    static readonly Serilog.ILogger Log = Serilog.Log.ForContext<Staticache>();

    public readonly static ConcurrentDictionary<string, CacheModel> cacheFiles = new();

    static readonly Timer cleanupTimer = new Timer(cleanup, null, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

    public static void Initialization()
    {
        var now = DateTimeOffset.Now;
        BucketFolders.Create("cache/static");

        foreach (string inFile in Directory.EnumerateFiles("cache/static", "*", SearchOption.AllDirectories))
        {
            try
            {
                // cacheKey-<time>.<type>
                ReadOnlySpan<char> fileName = inFile.AsSpan();
                int lastSlash = fileName.LastIndexOfAny('\\', '/');
                if (lastSlash >= 0)
                    fileName = fileName.Slice(lastSlash + 1);

                int dash = fileName.IndexOf('-');
                if (dash <= 0)
                {
                    deleteFile(inFile);
                    continue;
                }

                // '.' после дефиса
                int dot = fileName.Slice(dash + 1).IndexOf('.');
                if (dot < 0)
                {
                    deleteFile(inFile);
                    continue;
                }

                int firstDot = dash + 1 + dot;

                #region ex
                ReadOnlySpan<char> fileTimeSpan = fileName.Slice(dash + 1, firstDot - dash - 1);
                if (!long.TryParse(fileTimeSpan, out long fileTime) || fileTime == 0)
                {
                    deleteFile(inFile);
                    continue;
                }

                var ex = DateTimeOffset.FromUnixTimeMilliseconds(fileTime);
                if (now > ex)
                {
                    deleteFile(inFile);
                    continue;
                }
                #endregion

                // <type> = первое расширение после точки (игнорируем ".gz" и любые суффиксы)
                int typeEndRel = fileName.Slice(firstDot + 1).IndexOf('.');
                int typeEnd = typeEndRel < 0 ? fileName.Length : firstDot + 1 + typeEndRel;

                ReadOnlySpan<char> ext = fileName.Slice(firstDot + 1, typeEnd - (firstDot + 1));
                if (ext.Length == 0)
                {
                    deleteFile(inFile);
                    continue;
                }

                string cachekey = new string(fileName.Slice(0, dash));
                cacheFiles.TryAdd(cachekey, new(ex, ext.ToString()));
            }
            catch
            {
                deleteFile(inFile);
            }
        }
    }

    static void cleanup(object state)
    {
        try
        {
            var cutoff = DateTimeOffset.Now;

            foreach (var _c in cacheFiles)
            {
                if (_c.Value.ex > cutoff)
                    continue;

                if (cacheFiles.TryRemove(_c.Key, out _))
                {
                    string cachefile = GetFilePath(_c.Key, _c.Value.ex, _c.Value.ext);
                    deleteFile(cachefile);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CatchId={CatchId}", "id_h3352g2f");
        }
    }

    static void deleteFile(string file)
    {
        try
        {
            if (File.Exists(file))
                File.Delete(file);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CatchId={CatchId}", "id_wfl5s3rn");
        }
    }
    #endregion

    private readonly RequestDelegate _next;

    public Staticache(RequestDelegate next)
    {
        _next = next;
    }

    public Task Invoke(HttpContext httpContext)
    {
        var requestInfo = httpContext.Features.Get<RequestModel>();
        if (requestInfo.AesGcmKey != null || requestInfo.IsWsRequest || requestInfo.IsProxyRequest || requestInfo.IsProxyImg)
            return _next(httpContext);

        var endpoint = httpContext.GetEndpoint();
        var staticache = endpoint?.Metadata?.GetMetadata<StaticacheAttribute>();

        if (staticache == null)
            return _next(httpContext);

        var init = CoreInit.conf.Staticache;

        #region EventListener
        if (EventListener.Staticache != null)
        {
            var em = new EventStaticache(httpContext, requestInfo);

            foreach (Func<EventStaticache, bool> handler in EventListener.Staticache.GetInvocationList())
            {
                if (!handler(em))
                    return _next(httpContext);
            }
        }
        #endregion

        bool customRoute = false;
        StaticacheRoute route = default;

        #region init routes
        if (init.routes?.Count > 0 && init.enable)
        {
            foreach (var r in init.routes)
            {
                string path = httpContext.Request.Path.Value;

                if ((r.path != null && path.Equals(r.path))
                    || (r.pathRex != null && Regex.IsMatch(path, r.pathRex, RegexOptions.IgnoreCase)))
                {
                    customRoute = true;
                    route = r;
                    break;
                }
            }
        }
        #endregion

        if (customRoute == false)
        {
            // кеш отключён для всех кроме always
            if (!init.enable && !staticache.always)
                return _next(httpContext);

            // endpoint или настройки init требует явный routes
            if (staticache.manually || init.manually)
                return _next(httpContext);
        }

        if (init.minimalCacheMinutes > staticache.cacheMinutes)
            return _next(httpContext);

        if (init.disabledPaths != null && init.disabledPaths.Contains(httpContext.Request.Path.Value))
            return _next(httpContext);

        if (staticache.setHeadersNoCache)
        {
            httpContext.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate"; // HTTP 1.1.
            httpContext.Response.Headers["Pragma"] = "no-cache"; // HTTP 1.0.
            httpContext.Response.Headers["Expires"] = "0"; // Proxies.
        }

        if (customRoute == false)
            route = new();

        if (0 >= route.cacheMinutes)
            route.cacheMinutes = 1;

        var parameters = endpoint.Metadata
            .GetMetadata<ControllerActionDescriptor>()?
            .Parameters;

        string cachekey = getQueryKeys(httpContext, route.skipUids, parameters, route.queryKeys, route.ignoreQueryKeys);

        if (cacheFiles.TryGetValue(cachekey, out CacheModel _r))
        {
            httpContext.Response.Headers["X-StatiCache-Status"] = "HIT";

            httpContext.Response.ContentType = _r.ext switch
            {
                "html" => "text/html; charset=utf-8",
                "json" => "application/json; charset=utf-8",
                "js" => "application/javascript; charset=utf-8",
                "css" => "text/css; charset=utf-8",
                _ => "application/octet-stream"
            };

            string file = GetFilePath(cachekey, _r.ex, _r.ext);
            return httpContext.Response.SendFileAsync(file);
        }

        httpContext.Features.Set(new StaticacheFeature(route.cacheMinutes, cachekey));

        return _next(httpContext);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string getQueryKeys(HttpContext httpContext, bool skipUids, IList<ParameterDescriptor> parameters, string[] queryKeys, string[] ignoreQueryKeys)
    {
        var hash = Fnv1a.Empty;

        Fnv1a.Append(ref hash, httpContext.Request.Scheme);
        Fnv1a.Append(ref hash, httpContext.Request.Host.Value);
        Fnv1a.Append(ref hash, httpContext.Request.Path.Value);

        if (httpContext.Request.Query.TryGetValue("rjson", out StringValues rjson) && rjson.Count > 0)
            Fnv1a.Append(ref hash, rjson[0]);

        if (queryKeys != null && queryKeys.Length == 0)
        {
            foreach (string key in queryKeys)
                QueryAppend(ref hash, key, httpContext, skipUids, ignoreQueryKeys);
        }
        else if (parameters == null || parameters.Count == 0)
        {
            foreach (var param in parameters)
                QueryAppend(ref hash, param.Name, httpContext, skipUids, ignoreQueryKeys);
        }

        return Fnv1a.Base64Url(hash);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void QueryAppend(ref Fnv1aHash hash, string key, HttpContext httpContext, bool skipUids, string[] ignoreQueryKeys)
    {
        if (key == null)
            return;

        if (skipUids && CoreInit.SkipQueryKeys.Contains(key))
            return;

        if (ignoreQueryKeys != null && ignoreQueryKeys.Contains(key))
            return;

        if (httpContext.Request.Query.TryGetValue(key, out StringValues value) && value.Count > 0)
        {
            Fnv1a.Append(ref hash, key);
            Fnv1a.Append(ref hash, value[0]);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetFilePath(string cachekey, DateTimeOffset ex, string ext)
        => $"cache/static/{BucketFolders.Name(cachekey[0])}/{cachekey}-{ex.ToUnixTimeMilliseconds()}.{ext ?? "html"}";
}
