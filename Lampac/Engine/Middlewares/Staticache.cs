using Microsoft.AspNetCore.Http;
using Shared;
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

            return _next(httpContext);
        }
    }
}
