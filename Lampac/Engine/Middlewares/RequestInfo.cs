using Lampac.Engine.CORE;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Shared.Engine;
using Shared.Engine.CORE;
using Shared.Models;
using System.Collections.Generic;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class RequestInfo
    {
        #region RequestInfo
        static List<(IPAddress prefix, int prefixLength)> cloudflare_ips = null;

        private readonly RequestDelegate _next;
        public RequestInfo(RequestDelegate next)
        {
            _next = next;
        }
        #endregion

        async public Task InvokeAsync(HttpContext httpContext)
        {
            bool IsLocalRequest = false;
            string clientIp = httpContext.Connection.RemoteIpAddress.ToString();

            if (httpContext.Request.Headers.TryGetValue("localrequest", out var _localpasswd))
            {
                if (_localpasswd.ToString() != FileCache.ReadAllText("passwd"))
                {
                    await httpContext.Response.WriteAsync("error passwd", httpContext.RequestAborted);
                    return;
                }

                IsLocalRequest = true;

                if (httpContext.Request.Headers.TryGetValue("x-client-ip", out var xip) && !string.IsNullOrEmpty(xip))
                    clientIp = xip;
            }
            else if (AppInit.conf.real_ip_cf || AppInit.conf.frontend == "cloudflare")
            {
                #region cloudflare
                if (cloudflare_ips == null)
                {
                    string ips = await HttpClient.Get("https://www.cloudflare.com/ips-v4");
                    if (ips != null)
                    {
                        string ips_v6 = await HttpClient.Get("https://www.cloudflare.com/ips-v6");
                        if (ips_v6 != null)
                        {
                            foreach (string ip in (ips + "\n" + ips_v6).Split('\n'))
                            {
                                if (string.IsNullOrEmpty(ip) || !ip.Contains("/"))
                                    continue;

                                if (cloudflare_ips == null)
                                    cloudflare_ips = new List<(IPAddress prefix, int prefixLength)>();

                                string[] ln = ip.Split('/');
                                cloudflare_ips.Add((IPAddress.Parse(ln[0].Trim()), int.Parse(ln[1].Trim())));
                            }
                        }
                    }
                }

                if (cloudflare_ips != null)
                {
                    var clientIPAddress = IPAddress.Parse(clientIp);
                    foreach (var cf in cloudflare_ips)
                    {
                        if (new IPNetwork(cf.prefix, cf.prefixLength).Contains(clientIPAddress))
                        {
                            if (httpContext.Request.Headers.TryGetValue("CF-Connecting-IP", out var xip) && !string.IsNullOrEmpty(xip))
                                clientIp = xip;

                            if (httpContext.Request.Headers.TryGetValue("CF-Visitor", out var cfVisitor))
                            {
                                var visitorInfo = JsonNode.Parse(cfVisitor);
                                if (visitorInfo != null && visitorInfo["scheme"] != null)
                                    httpContext.Request.Scheme = visitorInfo["scheme"].ToString();
                            }

                            break;
                        }
                    }
                }
                #endregion
            }

            var req = new RequestModel()
            {
                IsLocalRequest = IsLocalRequest,
                IP = clientIp,
                UserAgent = httpContext.Request.Headers.UserAgent
            };

            if (!Regex.IsMatch(httpContext.Request.Path.Value, "^/(proxy-dash/|proxy/|proxyimg|lifeevents|externalids|ts|ws|weblog|rch/result|merchant/payconfirm|tmdb/)"))
                req.Country = GeoIP2.Country(req.IP);

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

                if (req.user != null)
                    req.@params = AppInit.conf.accsdb.@params;

                httpContext.Features.Set(req);
                await _next(httpContext);
            }
            else
            {
                string uid = Regex.Match(httpContext.Request.Host.Host, AppInit.conf.accsdb.domainId_pattern).Groups[1].Value;
                req.user = AppInit.conf.accsdb.findUser(uid);
                req.user_uid = uid;

                if (req.user == null)
                {
                    await httpContext.Response.WriteAsync("user not found", httpContext.RequestAborted);
                    return;
                }

                req.@params = AppInit.conf.accsdb.@params;

                httpContext.Features.Set(req);
                await _next(httpContext);
            }
        }
    }
}
