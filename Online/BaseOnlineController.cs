using Lampac.Engine;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;

namespace Online
{
    public class BaseOnlineController : BaseController
    {
        public ActionResult OnError()
        {
            return Content(string.Empty, "text/html; charset=utf-8");
        }

        public ActionResult OnError(ProxyManager proxyManager, bool refresh_proxy = true)
        {
            if (refresh_proxy)
                proxyManager?.Refresh();

            return OnError();
        }
    }
}
