using Microsoft.AspNetCore.Http;
using Microsoft.CodeAnalysis.Scripting;
using Shared;
using Shared.Engine;
using Shared.Models;
using Shared.Models.CSharpGlobals;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class OverrideResponse
    {
        private readonly RequestDelegate _next;
        private readonly bool first;
        public OverrideResponse(RequestDelegate next, bool first)
        {
            _next = next;
            this.first = first;
        }

        public Task Invoke(HttpContext httpContext)
        {
            if (AppInit.conf.overrideResponse != null && AppInit.conf.overrideResponse.Count > 0)
            {
                var requestInfo = httpContext.Features.Get<RequestModel>();
                if (requestInfo.IsLocalRequest)
                    return _next(httpContext);

                string url = httpContext.Request.Path.Value + httpContext.Request.QueryString.Value;

                foreach (var over in AppInit.conf.overrideResponse.Where(i => i.firstEndpoint == first))
                {
                    if (Regex.IsMatch(url, over.pattern, RegexOptions.IgnoreCase))
                    {
                        switch (over.action)
                        {
                            case "html":
                                {
                                    httpContext.Response.ContentType = over.type;
                                    return httpContext.Response.WriteAsync(over.val.Replace("{localhost}", AppInit.Host(httpContext)), httpContext.RequestAborted);
                                }
                            case "file":
                                {
                                    httpContext.Response.ContentType = over.type;

                                    if (string.IsNullOrEmpty(over.val) || !File.Exists(over.val))
                                    {                                         
                                        httpContext.Response.StatusCode = 404;
                                        return Task.CompletedTask;
                                    }

                                    if (Regex.IsMatch(over.val, "\\.(html|txt|css|js|json|xml)$", RegexOptions.IgnoreCase))
                                    {
                                        string val = FileCache.ReadAllText(over.val);
                                        return httpContext.Response.WriteAsync(val.Replace("{localhost}", AppInit.Host(httpContext)), httpContext.RequestAborted);
                                    }
                                    else
                                    {
                                        return httpContext.Response.SendFileAsync(over.val);
                                    }
                                }
                            case "redirect":
                                {
                                    httpContext.Response.Redirect(over.val);
                                    return Task.CompletedTask;
                                }
                            case "eval":
                                {
                                    var options = ScriptOptions.Default
                                        .AddReferences(typeof(Console).Assembly).AddImports("System")
                                        .AddReferences(typeof(Regex).Assembly).AddImports("System.Text.RegularExpressions");

                                    bool _next = CSharpEval.BaseExecute<bool>(over.val, new OverrideResponseGlobals(url, httpContext.Request, requestInfo), options);
                                    if (!_next)
                                        return Task.CompletedTask;
                                    break;
                                }
                            default:
                                break;
                        }
                    }
                }
            }

            return _next(httpContext);
        }
    }
}
