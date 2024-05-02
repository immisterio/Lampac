using Microsoft.AspNetCore.Builder;

namespace Lampac.Engine.Middlewares
{
    public static class Extensions
    {
        public static IApplicationBuilder UseModHeaders(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ModHeaders>();
        }

        public static IApplicationBuilder UseOverrideResponse(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<OverrideResponse>();
        }

        public static IApplicationBuilder UseAccsdb(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<Accsdb>();
        }

        public static IApplicationBuilder UseProxyAPI(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ProxyAPI>();
        }

        public static IApplicationBuilder UseProxyIMG(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ProxyImg>();
        }

        public static IApplicationBuilder UseModule(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<Module>();
        }

        public static IApplicationBuilder UseCache(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<Cache>();
        }
    }
}
