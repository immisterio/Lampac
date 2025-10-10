using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine;

namespace Shared.Models.CSharpGlobals
{
    public record CmdEvalModel(string key, string comand, RequestModel requestInfo, HttpRequest request, HybridCache hybridCache, IMemoryCache memoryCache);
}
