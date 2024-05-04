using Microsoft.AspNetCore.Http;
using Shared.Engine;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class OverrideResponse
    {
        private readonly RequestDelegate _next;
        public OverrideResponse(RequestDelegate next)
        {
            _next = next;
        }

        async public Task InvokeAsync(HttpContext httpContext)
        {
            if (AppInit.conf.overrideResponse != null && AppInit.conf.overrideResponse.Count > 0)
            {
                string url = httpContext.Request.Path.Value + httpContext.Request.QueryString.Value;

                foreach (var over in AppInit.conf.overrideResponse)
                {
                    if (Regex.IsMatch(url, over.pattern, RegexOptions.IgnoreCase))
                    {
                        switch (over.action)
                        {
                            case "html":
                                {
                                    httpContext.Response.ContentType = over.type;
                                    await httpContext.Response.WriteAsync(over.val.Replace("{localhost}", AppInit.Host(httpContext)));
                                    return;
                                }
                            case "file":
                                {
                                    httpContext.Response.ContentType = over.type;
                                    if (Regex.IsMatch(over.val, "\\.(html|txt|css|js|json|xml)$", RegexOptions.IgnoreCase))
                                    {
                                        string val = FileCache.ReadAllText(over.val);
                                        await httpContext.Response.WriteAsync(val.Replace("{localhost}", AppInit.Host(httpContext)));
                                    }
                                    else
                                    {
                                        using (var fs = new FileStream(over.val, FileMode.Open))
                                            await fs.CopyToAsync(httpContext.Response.Body);
                                    }
                                    return;
                                }
                            case "redirect":
                                {
                                    httpContext.Response.Redirect(over.val);
                                    return;
                                }
                            default:
                                break;
                        }
                    }
                }
            }

            await _next(httpContext);
        }
    }
}
