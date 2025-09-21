using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.Memory;

namespace Shared
{
    public class Startup
    {
        public static bool IsShutdown { get; set; }

        public static IServiceProvider ApplicationServices { get; private set; }

        public static IMemoryCache memoryCache { get; private set; }

        public static void Configure(IApplicationBuilder app, IMemoryCache mem)
        {
            ApplicationServices = app.ApplicationServices;
            memoryCache = mem;
        }
    }
}
