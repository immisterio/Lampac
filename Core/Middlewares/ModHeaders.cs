using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Core.Middlewares;

public class ModHeaders
{
    private readonly RequestDelegate _next;
    public ModHeaders(RequestDelegate next)
    {
        _next = next;
    }

    public Task Invoke(HttpContext httpContext)
    {
        if (httpContext.Request.Path.Value == "/cors/check")
            return Task.CompletedTask;

        httpContext.Response.Headers["Access-Control-Allow-Credentials"] = "true";
        httpContext.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
        httpContext.Response.Headers["Access-Control-Allow-Methods"] = "POST, GET, OPTIONS";

        if (httpContext.Request.Headers.TryGetValue("Access-Control-Request-Headers", out var allowHeaders))
            httpContext.Response.Headers["Access-Control-Allow-Headers"] = allowHeaders;
        else
            httpContext.Response.Headers["Access-Control-Allow-Headers"] = "*";

        if (httpContext.Request.Headers.TryGetValue("origin", out StringValues origin) && origin.Count > 0)
            httpContext.Response.Headers["Access-Control-Allow-Origin"] = GetOrigin(origin[0]);
        else if (httpContext.Request.Headers.TryGetValue("referer", out StringValues referer) && referer.Count > 0)
            httpContext.Response.Headers["Access-Control-Allow-Origin"] = GetOrigin(referer[0]);
        else
            httpContext.Response.Headers["Access-Control-Allow-Origin"] = "*";

        if (HttpMethods.IsOptions(httpContext.Request.Method))
            return Task.CompletedTask;

        return _next(httpContext);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static string GetOrigin(string url)
    {
        if (string.IsNullOrEmpty(url))
            return string.Empty;

        int scheme = url.IndexOf("://", StringComparison.Ordinal);
        if (scheme <= 0)
            return url;

        ReadOnlySpan<char> urlSpan = url.AsSpan();

        int start = scheme + 3;
        int slash = urlSpan.Slice(start).IndexOfAny('/', '?', '#');

        if (slash < 0)
            return url; // уже origin

        return url.Substring(0, start + slash);
    }
}
