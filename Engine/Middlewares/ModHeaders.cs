using Microsoft.AspNetCore.Http;
using System;
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
            if (!string.IsNullOrWhiteSpace(AppInit.conf.apikey))
            {
                if (Regex.IsMatch(httpContext.Request.Path.Value, "^/(api/v2.0/indexers|lite/jac|toloka|rutracker|nnmclub|kinozal|bitru|selezen|megapeer|animelayer|anilibria)"))
                {
                    if (AppInit.conf.apikey != Regex.Match(httpContext.Request.QueryString.Value, "(\\?|&)apikey=([^&]+)").Groups[2].Value)
                        return Task.CompletedTask;
                }
            }

            httpContext.Response.Headers.Add("Access-Control-Allow-Headers", "Accept, Content-Type");
            httpContext.Response.Headers.Add("Access-Control-Allow-Methods", "POST, GET");
            httpContext.Response.Headers.Add("Access-Control-Allow-Origin", "*");

            if (Regex.IsMatch(httpContext.Request.Path.Value, "^/(lampainit|sisi|lite|online|tmdbproxy|tracks|dlna)\\.js"))
            {
                if (string.IsNullOrWhiteSpace(httpContext.Request.QueryString.Value))
                {
                    httpContext.Response.Redirect(httpContext.Request.Path.Value + "?v=" + DateTime.Now.ToBinary().ToString().Replace("-", ""));
                    return Task.CompletedTask;
                }
            }

            return _next(httpContext);
        }
    }
}
