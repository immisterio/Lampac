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
            if (!string.IsNullOrWhiteSpace(AppInit.conf.apikey))
            {
                if (Regex.IsMatch(httpContext.Request.Path.Value, "^/(api/v2.0/indexers|lite/jac|toloka|rutracker|nnmclub|kinozal|bitru)"))
                {
                    if (AppInit.conf.apikey != Regex.Match(httpContext.Request.QueryString.Value, "(\\?|&)apikey=([^&]+)").Groups[2].Value)
                        return Task.CompletedTask;
                }
            }

            httpContext.Response.Headers.Add("Access-Control-Allow-Headers", "Accept, Content-Type");
            httpContext.Response.Headers.Add("Access-Control-Allow-Methods", "POST, GET");
            httpContext.Response.Headers.Add("Access-Control-Allow-Origin", "*");

            return _next(httpContext);
        }
    }
}
