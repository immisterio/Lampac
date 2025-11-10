using Microsoft.AspNetCore.Builder;

namespace Lampac.Engine.Middlewares
{
    public static class Extensions
    {
        public static IApplicationBuilder UseWAF(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<WAF>();
        }

        public static IApplicationBuilder UseModHeaders(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ModHeaders>();
        }

        public static IApplicationBuilder UseRequestStatistics(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestStatistics>();
        }

        public static IApplicationBuilder UseOverrideResponse(this IApplicationBuilder builder, bool first)
        {
            return builder.UseMiddleware<OverrideResponse>(first);
        }

        public static IApplicationBuilder UseRequestInfo(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestInfo>();
        }

        public static IApplicationBuilder UseAnonymousRequest(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AnonymousRequest>();
        }

        public static IApplicationBuilder UseAlwaysRjson(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AlwaysRjson>();
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

        public static IApplicationBuilder UseProxyCub(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ProxyCub>();
        }

        public static IApplicationBuilder UseProxyTmdb(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ProxyTmdb>();
        }

        public static IApplicationBuilder UseModule(this IApplicationBuilder builder, bool first)
        {
            return builder.UseMiddleware<Module>(first);
        }
    }
}
