using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using Shared.Models.Base;

namespace Shared.Models.Events
{
    public record EventLoadKit(BaseSettings init, JObject userconf);

    public record EventBadInitialization(BaseSettings init, bool? rch, RequestModel requestInfo, string host, HttpContext HttpContext, IMemoryCache memoryCache);
}
