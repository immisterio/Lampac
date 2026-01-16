using Microsoft.AspNetCore.Http;
using Shared;
using Shared.Models;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class Staticache
    {
        private readonly RequestDelegate _next;

        public Staticache(RequestDelegate next)
        {
            _next = next;
        }

        public Task Invoke(HttpContext httpContext)
        {
            if (AppInit.conf.Staticache.enable != true)
                return _next(httpContext);

            var requestInfo = httpContext.Features.Get<RequestModel>();

            if (InvkEvent.IsStaticache())
            {
                bool next = InvkEvent.Staticache(new Shared.Models.Events.EventStaticache(httpContext, requestInfo));
                if (next)
                    return _next(httpContext);

                return Task.CompletedTask;
            }

            return _next(httpContext);
        }
    }
}
