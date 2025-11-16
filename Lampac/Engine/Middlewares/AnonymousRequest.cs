using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Shared.Models;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class AnonymousRequest
    {
        static bool manifestInitial = false;

        private readonly RequestDelegate _next;
        public AnonymousRequest(RequestDelegate next)
        {
            _next = next;
        }

        public Task Invoke(HttpContext httpContext)
        {
            var requestInfo = httpContext.Features.Get<RequestModel>();

            if (!manifestInitial)
            {
                if (!File.Exists("module/manifest.json"))
                {
                    if (httpContext.Request.Path.Value.StartsWith("/admin/manifest/install"))
                    {
                        requestInfo.IsAnonymousRequest = true;
                        httpContext.Features.Set(requestInfo);
                        return _next(httpContext);
                    }

                    httpContext.Response.Redirect("/admin/manifest/install");
                    return Task.CompletedTask;
                }
                else { manifestInitial = true; }
            }

            var endpoint = httpContext.GetEndpoint();
            if (endpoint != null && endpoint.Metadata.GetMetadata<IAllowAnonymous>() != null)
                requestInfo.IsAnonymousRequest = true;

            if (httpContext.Request.Path.Value == "/" || httpContext.Request.Path.Value == "/favicon.ico")
                requestInfo.IsAnonymousRequest = true;

            if (httpContext.Request.Path.Value == "/.well-known/appspecific/com.chrome.devtools.json")
                requestInfo.IsAnonymousRequest = true;

            if (httpContext.Request.Path.Value.EndsWith("/personal.lampa"))
                requestInfo.IsAnonymousRequest = true;

            if (Regex.IsMatch(httpContext.Request.Path.Value, "^/(merchant/payconfirm|streampay|b2pay|cryptocloud|freekassa|litecoin)(/|$)", RegexOptions.IgnoreCase))
                requestInfo.IsAnonymousRequest = true;

            if (Regex.IsMatch(httpContext.Request.Path.Value, "^/(proxy-dash|cub|corseu|media|ts|kit|bind)(/|$)", RegexOptions.IgnoreCase))
                requestInfo.IsAnonymousRequest = true;

            if (Regex.IsMatch(httpContext.Request.Path.Value, "^/(api/chromium|headers|myip|geo|version|weblog|stats|rch|ping|extensions)(/|$)", RegexOptions.IgnoreCase))
                requestInfo.IsAnonymousRequest = true;

            if (Regex.IsMatch(httpContext.Request.Path.Value, "^/(on/|(lite|online|sisi|timecode|bookmark|sync|tmdbproxy|dlna|ts|tracks|transcoding|backup|catalog|invc-ws)/js/)", RegexOptions.IgnoreCase))
                requestInfo.IsAnonymousRequest = true;

            if (Regex.IsMatch(httpContext.Request.Path.Value, "^/lite/(withsearch|filmixpro|fxapi/lowlevel|kinopubpro|vokinotk|rhs/bind|iptvonline/bind|getstv/bind)(/|$)", RegexOptions.IgnoreCase))
                requestInfo.IsAnonymousRequest = true;

            if (Regex.IsMatch(httpContext.Request.Path.Value, "^/(([^/]+/)?app\\.min\\.js|([^/]+/)?css/app\\.css|[a-zA-Z\\-]+\\.js|msx/start\\.json|samsung\\.wgt)", RegexOptions.IgnoreCase))
                requestInfo.IsAnonymousRequest = true;

            httpContext.Features.Set(requestInfo);
            return _next(httpContext);
        }
    }
}
