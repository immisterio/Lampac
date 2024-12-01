using Microsoft.AspNetCore.Http;
using Shared.Engine;
using Shared.Engine.CORE;
using Shared.Models;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class RequestInfo
    {
        private readonly RequestDelegate _next;
        public RequestInfo(RequestDelegate next)
        {
            _next = next;
        }

        public Task Invoke(HttpContext httpContext)
        {
            bool IsLocalRequest = false;
            if (httpContext.Request.Headers.TryGetValue("localrequest", out var _localpasswd))
            {
                if (_localpasswd.ToString() != FileCache.ReadAllText("passwd"))
                    return httpContext.Response.WriteAsync("error passwd", httpContext.RequestAborted);

                IsLocalRequest = true;
            }

            var req = new RequestModel()
            {
                IsLocalRequest = IsLocalRequest,
                IP = httpContext.Connection.RemoteIpAddress.ToString(),
                UserAgent = httpContext.Request.Headers.UserAgent
            };

            if (!Regex.IsMatch(httpContext.Request.Path.Value, "^/(proxy-dash/|proxy/|proxyimg|lifeevents|externalids|ts|ws|weblog|rch/result|merchant/payconfirm)"))
                req.Country = GeoIP2.Country(httpContext.Connection.RemoteIpAddress.ToString());

            if (string.IsNullOrEmpty(AppInit.conf.accsdb.domainId_pattern))
            {
                #region getuid
                string getuid()
                {
                    if (!string.IsNullOrEmpty(httpContext.Request.Query["token"].ToString()))
                        return httpContext.Request.Query["token"].ToString();

                    if (!string.IsNullOrEmpty(httpContext.Request.Query["account_email"].ToString()))
                        return httpContext.Request.Query["account_email"].ToString();

                    if (!string.IsNullOrEmpty(httpContext.Request.Query["uid"].ToString()))
                        return httpContext.Request.Query["uid"].ToString();

                    if (!string.IsNullOrEmpty(httpContext.Request.Query["box_mac"].ToString()))
                        return httpContext.Request.Query["box_mac"].ToString();

                    return null;
                }
                #endregion

                req.user = AppInit.conf.accsdb.findUser(httpContext, out string uid);
                req.user_uid = uid;

                if (string.IsNullOrEmpty(req.user_uid))
                    req.user_uid = getuid();

                httpContext.Features.Set(req);
                return _next(httpContext);
            }
            else
            {
                string uid = Regex.Match(httpContext.Request.Host.Host, AppInit.conf.accsdb.domainId_pattern).Groups[1].Value;
                req.user = AppInit.conf.accsdb.findUser(uid);
                req.user_uid = uid;

                if (req.user == null)
                    return httpContext.Response.WriteAsync("user not found", httpContext.RequestAborted);

                httpContext.Features.Set(req);
                return _next(httpContext);
            }
        }
    }
}
