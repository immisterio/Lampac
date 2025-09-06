using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using Shared.Engine;
using Shared.Models.Base;

namespace Shared.Models.Events
{
    public record EventLoadKit(BaseSettings defaultinit, BaseSettings init, JObject userconf, RequestModel requestInfo, HybridCache hybridCache);

    public record EventMiddleware(RequestDelegate _next, RequestModel requestInfo, HttpRequest request, HttpContext httpContext, HybridCache hybridCache, IMemoryCache memoryCache);

    public record EventBadInitialization(BaseSettings init, bool? rch, RequestModel requestInfo, string host, HttpRequest request, HybridCache hybridCache);

    public record EventAppReplace(string source, string token, string arg, string host, RequestModel requestInfo, HttpRequest request, HybridCache hybridCache);
}
