using Microsoft.AspNetCore.Http;
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
        if (httpContext.Request.Path.Value.StartsWith("/cors/check", StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        httpContext.Response.Headers["Access-Control-Allow-Credentials"] = "true";
        httpContext.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
        httpContext.Response.Headers["Access-Control-Allow-Methods"] = "POST, GET, OPTIONS";

        if (httpContext.Request.Headers.TryGetValue("Access-Control-Request-Headers", out var allowHeaders))
            httpContext.Response.Headers["Access-Control-Allow-Headers"] = allowHeaders;
        else
            httpContext.Response.Headers["Access-Control-Allow-Headers"] = "*";

        if (httpContext.Request.Headers.TryGetValue("origin", out var origin))
            httpContext.Response.Headers["Access-Control-Allow-Origin"] = GetOrigin(origin);
        else if (httpContext.Request.Headers.TryGetValue("referer", out var referer))
            httpContext.Response.Headers["Access-Control-Allow-Origin"] = GetOrigin(referer);
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

        int start = scheme + 3;
        int slash = url.IndexOf('/', start);
        if (slash < 0)
            return url; // уже origin

        return url.Substring(0, slash);
    }
}
