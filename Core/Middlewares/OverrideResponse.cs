using Microsoft.AspNetCore.Http;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Caching.Memory;
using Shared;
using Shared.Models.Base;
using Shared.Models.CSharpGlobals;
using Shared.Services;
using Shared.Services.Hybrid;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Core.Middlewares;

public class OverrideResponse
{
    #region static
    public readonly static ScriptOptions evalOptions = ScriptOptions.Default
        .AddReferences(typeof(Console).Assembly).AddImports("System")
        .AddReferences(typeof(Regex).Assembly).AddImports("System.Text.RegularExpressions");

    private readonly RequestDelegate _next;
    private readonly bool first;

    public OverrideResponse(RequestDelegate next, bool first)
    {
        _next = next;
        this.first = first;
    }
    #endregion

    public Task Invoke(HttpContext httpContext)
    {
        var memoryCache = HybridCache.GetMemory();

        foreach (var over in CoreInit.conf.overrideResponse)
        {
            if (over.firstEndpoint != first)
                continue;

            bool isMatch = (over.path != null && httpContext.Request.Path.Value.Equals(over.path))
                || (over.pattern != null && Regex.IsMatch(httpContext.Request.Path.Value, over.pattern, RegexOptions.IgnoreCase));

            if (isMatch)
            {
                switch (over.action)
                {
                    case "html":
                        {
                            httpContext.Response.ContentType = over.type;

                            if (over.val.Contains("{localhost}"))
                                return httpContext.Response.WriteAsync(over.val.Replace("{localhost}", CoreInit.Host(httpContext)));

                            return httpContext.Response.WriteAsync(over.val, httpContext.RequestAborted);
                        }
                    case "file":
                        {
                            httpContext.Response.ContentType = over.type;

                            if (IsTextFile(over.val))
                            {
                                string host = CoreInit.Host(httpContext);
                                string memKey = $"OverrideResponse:{over.val}:{host}";

                                if (!memoryCache.TryGetValue(memKey, out string file))
                                {
                                    file = FileCache.ReadAllText(over.val);
                                    file = file.Replace("{localhost}", CoreInit.Host(httpContext));
                                    memoryCache.Set(memKey, file, TimeSpan.FromHours(1));
                                }

                                return httpContext.Response.WriteAsync(file, httpContext.RequestAborted);
                            }
                            else
                            {
                                return httpContext.Response.SendFileAsync(over.val, httpContext.RequestAborted);
                            }
                        }
                    case "redirect":
                        {
                            httpContext.Response.Redirect(over.val);
                            return Task.CompletedTask;
                        }
                    case "eval":
                        {
                            var requestInfo = httpContext.Features.Get<RequestModel>();
                            string url = httpContext.Request.Path.Value + httpContext.Request.QueryString.Value;
                            bool _next = CSharpEval.BaseExecute<bool>(over.val, new OverrideResponseGlobals(url, httpContext.Request, requestInfo), evalOptions);
                            if (!_next)
                                return Task.CompletedTask;
                            break;
                        }
                    default:
                        break;
                }
            }
        }

        return _next(httpContext);
    }

    static bool IsTextFile(string path)
    {
        ReadOnlySpan<char> ext = path.AsSpan();
        int lastDot = ext.LastIndexOf('.');
        if (lastDot == -1)
            return false;

        ext = ext.Slice(lastDot);

        return ext.StartsWith(".html", StringComparison.OrdinalIgnoreCase)
            || ext.StartsWith(".txt", StringComparison.OrdinalIgnoreCase)
            || ext.StartsWith(".css", StringComparison.OrdinalIgnoreCase)
            || ext.StartsWith(".js", StringComparison.OrdinalIgnoreCase) // захватывает и .json
            || ext.StartsWith(".xml", StringComparison.OrdinalIgnoreCase);
    }
}
