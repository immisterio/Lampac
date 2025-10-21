using Microsoft.AspNetCore.Http;
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

            string allowHeaders = "Accept, Origin, Content-Type, Authorization, X-Requested-With, X-Signalr-User-Agent, Cache-Control, DNT, If-Modified-Since, Keep-Alive, User-Agent, Token, Profile";
            if (httpContext.Request.Headers.TryGetValue("Access-Control-Request-Headers", out var accessHeaders) && !string.IsNullOrEmpty(accessHeaders.ToString()))
                allowHeaders += ", " + accessHeaders.ToString();

            httpContext.Response.Headers["Access-Control-Allow-Headers"] = allowHeaders;

            if (httpContext.Request.Headers.TryGetValue("origin", out var origin))
                httpContext.Response.Headers["Access-Control-Allow-Origin"] = origin.ToString();
            else if (httpContext.Request.Headers.TryGetValue("referer", out var referer))
                httpContext.Response.Headers["Access-Control-Allow-Origin"] = referer.ToString();
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
    }
}
