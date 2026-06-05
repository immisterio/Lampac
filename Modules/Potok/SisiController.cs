using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Attributes;
using Shared.Services;

namespace Potok;

public class SisiController : BaseController
{
    [HttpGet, AllowAnonymous]
    [Staticache(5, always: true, setHeadersNoCache: true)]
    [Route("blue-oyster/{file}")]
    public ActionResult Index(string file)
    {
        string plugin = FileCache.ReadAllText($"{ModInit.modpath}/the-blue-oyster/{file}", $"blue-oyster_{file}", saveCache: false)
            .Replace("{localhost}", host);

        string type = file.EndsWith(".json")
            ? "application/json; charset=utf-8"
            : "application/javascript; charset=utf-8";

        return ContentTo(plugin, type);
    }
}
