using Microsoft.AspNetCore.Mvc;
using Shared.Models;
using Shared.Models.Events;

namespace Shared
{
    public class EventListener
    {
        public static Action<EventLoadKit> LoadKit;

        public static Func<EventProxyApiCreateHttpRequest, Task> ProxyApiCreateHttpRequest;

        public static Func<EventBadInitialization, Task<ActionResult>> BadInitialization;

        public static Func<EventHostStreamProxy, string> HostStreamProxy;

        public static Func<EventMyLocalIp, Task<string>> MyLocalIp;

        public static Func<EventControllerHttpHeaders, List<HeadersModel>> HttpHeaders;

        public static Func<bool, EventMiddleware, Task<bool>> Middleware;

        public static Func<string, EventAppReplace, string> AppReplace;
    }
}
