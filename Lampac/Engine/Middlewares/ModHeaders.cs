using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class ModHeaders
    {
        private readonly RequestDelegate _next;
        public ModHeaders(RequestDelegate next)
        {
            _next = next;
        }

        public Task Invoke(HttpContext httpContext)
        {
            if (httpContext.Request.Path.Value.StartsWith("/cors/check"))
                return Task.CompletedTask;

            httpContext.Response.Headers["Access-Control-Allow-Credentials"] = "true";
            httpContext.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
            httpContext.Response.Headers["Access-Control-Allow-Methods"] = "POST, GET, OPTIONS";

            if (GetAllowHeaders(httpContext, out HashSet<string> allowHeadersSet))
                httpContext.Response.Headers["Access-Control-Allow-Headers"] = string.Join(", ", allowHeadersSet);
            else
                httpContext.Response.Headers["Access-Control-Allow-Headers"] = stringAllowHeaders;

            if (httpContext.Request.Headers.TryGetValue("origin", out var origin))
                httpContext.Response.Headers["Access-Control-Allow-Origin"] = GetOrigin(origin);
            else if (httpContext.Request.Headers.TryGetValue("referer", out var referer))
                httpContext.Response.Headers["Access-Control-Allow-Origin"] = GetOrigin(referer);
            else
                httpContext.Response.Headers["Access-Control-Allow-Origin"] = "*";

            if (Regex.IsMatch(httpContext.Request.Path.Value, "^/(lampainit|sisi|lite|online|tmdbproxy|cubproxy|tracks|transcoding|dlna|timecode|bookmark|catalog|sync|backup|ts|invc-ws)\\.js") ||
                Regex.IsMatch(httpContext.Request.Path.Value, "^/(on/|(lite|online|sisi|timecode|bookmark|sync|tmdbproxy|dlna|ts|tracks|transcoding|backup|catalog|invc-ws)/js/)"))
            {
                httpContext.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate"; // HTTP 1.1.
                httpContext.Response.Headers["Pragma"] = "no-cache"; // HTTP 1.0.
                httpContext.Response.Headers["Expires"] = "0"; // Proxies.
            }

            if (HttpMethods.IsOptions(httpContext.Request.Method))
                return Task.CompletedTask;

            return _next(httpContext);
        }


        static readonly string stringAllowHeaders = "Authorization, Token, Profile, Content-Type, X-Signalr-User-Agent, X-Requested-With";

        static readonly HashSet<string> hashAllowHeaders = new HashSet<string>(
        [
            "Authorization", "Token", "Profile",
            "Content-Type", "X-Signalr-User-Agent", "X-Requested-With"
        ], StringComparer.OrdinalIgnoreCase);


        static bool GetAllowHeaders(HttpContext httpContext, out HashSet<string> headersSet)
        {
            if (httpContext.Request.Headers.TryGetValue("Access-Control-Request-Headers", out var requestedHeaders))
            {
                headersSet = [.. hashAllowHeaders];

                foreach (var header in requestedHeaders.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var h = header.Trim();
                    if (!string.IsNullOrEmpty(h))
                        headersSet.Add(h);
                }

                return true;
            }

            headersSet = null;
            return false;
        }

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
}
