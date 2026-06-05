using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Attributes;
using Shared.Services;

namespace Potok;

public class OnlineController : BaseController
{
    [HttpGet, AllowAnonymous]
    [Staticache(5, always: true, setHeadersNoCache: true)]
    [Route("naked-gun/{file}")]
    public ActionResult Index(string file)
    {
        string plugin = FileCache.ReadAllText($"{ModInit.modpath}/the-naked-gun/{file}", $"naked-gun_{file}", saveCache: false)
            .Replace("{localhost}", host);

        return ContentTo(plugin);
    }
}
