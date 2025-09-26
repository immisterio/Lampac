using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Shared;
using Shared.Models;
using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class RequestInfo
    {
        #region RequestInfo
        private readonly RequestDelegate _next;
        IMemoryCache memoryCache;

        public RequestInfo(RequestDelegate next, IMemoryCache mem)
        {
            _next = next;
            memoryCache = mem;
        }
        #endregion

        public Task Invoke(HttpContext httpContext)
        {
            #region stats
            if (AppInit.conf.openstat.enable)
            {
                string skey = $"stats:request:{DateTime.Now.Minute}";
                if (!memoryCache.TryGetValue(skey, out long _req))
                    _req = 0;

                _req++;
                memoryCache.Set(skey, _req, DateTime.Now.AddMinutes(59));
            }
            #endregion

            bool IsLocalRequest = false;
            string cf_country = null;
            string clientIp = httpContext.Connection.RemoteIpAddress.ToString();

            if (httpContext.Request.Headers.TryGetValue("localrequest", out var _localpasswd))
            {
                if (_localpasswd.ToString() != AppInit.rootPasswd)
                    return httpContext.Response.WriteAsync("error passwd", httpContext.RequestAborted);

                IsLocalRequest = true;

                if (httpContext.Request.Headers.TryGetValue("x-client-ip", out var xip) && !string.IsNullOrEmpty(xip))
                    clientIp = xip;
            }
            else if (AppInit.conf.real_ip_cf || AppInit.conf.listen.frontend == "cloudflare")
            {
                #region cloudflare
                if (Program.cloudflare_ips != null && Program.cloudflare_ips.Count > 0)
                {
                    try
                    {
                        var clientIPAddress = IPAddress.Parse(clientIp);
                        foreach (var cf in Program.cloudflare_ips)
                        {
                            if (new System.Net.IPNetwork(cf.prefix, cf.prefixLength).Contains(clientIPAddress))
                            {
                                if (httpContext.Request.Headers.TryGetValue("CF-Connecting-IP", out var xip) && !string.IsNullOrEmpty(xip))
                                    clientIp = xip;

                                if (httpContext.Request.Headers.TryGetValue("X-Forwarded-Proto", out var xfp) && !string.IsNullOrEmpty(xfp))
                                {
                                    if (xfp == "http" || xfp == "https")
                                        httpContext.Request.Scheme = xfp;
                                }

                                if (httpContext.Request.Headers.TryGetValue("CF-IPCountry", out var xcountry) && !string.IsNullOrEmpty(xcountry))
                                    cf_country = xcountry;

                                break;
                            }
                        }
                    }
                    catch { }
                }
                #endregion
            }
            // запрос с cloudflare, запрос не в админку
            else if (httpContext.Request.Headers.ContainsKey("CF-Connecting-IP") && !httpContext.Request.Path.Value.StartsWith("/admin"))
            {
                // если не указан frontend и это не первоначальная установка, тогда выводим ошибку
                if (string.IsNullOrEmpty(AppInit.conf.listen.frontend) && File.Exists("module/manifest.json"))
                    return httpContext.Response.WriteAsync(unknownFrontend, httpContext.RequestAborted);
            }

            var req = new RequestModel()
            {
                IsLocalRequest = IsLocalRequest,
                IP = clientIp,
                Country = cf_country,
                Path = httpContext.Request.Path.Value,
                Query = httpContext.Request.QueryString.Value,
                UserAgent = httpContext.Request.Headers.UserAgent
            };

            #region Weblog Request
            if (!IsLocalRequest && nws.weblog_clients.Count > 0)
            {
                var logBuilder = new System.Text.StringBuilder();
                logBuilder.AppendLine($"{DateTime.Now}");
                logBuilder.AppendLine($"IP: {clientIp} {req.Country}");
                logBuilder.AppendLine($"URL: {AppInit.Host(httpContext)}{httpContext.Request.Path}{httpContext.Request.QueryString}\n");

                foreach (var header in httpContext.Request.Headers)
                    logBuilder.AppendLine($"{header.Key}: {header.Value}");

                //Console.WriteLine(logBuilder.ToString());
                nws.SendLog(logBuilder.ToString(), "request");
            }
            #endregion

            if (!string.IsNullOrEmpty(AppInit.conf.accsdb.domainId_pattern))
            {
                string uid = Regex.Match(httpContext.Request.Host.Host, AppInit.conf.accsdb.domainId_pattern).Groups[1].Value;
                req.user = AppInit.conf.accsdb.findUser(uid);
                req.user_uid = uid;

                if (req.user == null)
                    return httpContext.Response.WriteAsync("user not found", httpContext.RequestAborted);

                req.@params = AppInit.conf.accsdb.@params;

                httpContext.Features.Set(req);
                return _next(httpContext);
            }
            else
            {
                #region getuid
                string getuid()
                {
                    if (httpContext.Request.Query.ContainsKey("token"))
                    {
                        string val = httpContext.Request.Query["token"].ToString();
                        if (!string.IsNullOrEmpty(val))
                            return val;
                    }

                    if (httpContext.Request.Query.ContainsKey("account_email"))
                    {
                        string val = httpContext.Request.Query["account_email"].ToString();
                        if (!string.IsNullOrEmpty(val))
                            return val;
                    }

                    if (httpContext.Request.Query.ContainsKey("uid"))
                    {
                        string val = httpContext.Request.Query["uid"].ToString();
                        if (!string.IsNullOrEmpty(val))
                            return val;
                    }

                    if (httpContext.Request.Query.ContainsKey("box_mac"))
                    {
                        string val = httpContext.Request.Query["box_mac"].ToString();
                        if (!string.IsNullOrEmpty(val))
                            return val;
                    }

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
                return _next(httpContext);
            }
        }


        static string unknownFrontend = @"<!DOCTYPE html>
<html lang='ru'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>CloudFlare</title>
    <link href='/control/npm/bootstrap.min.css' rel='stylesheet'>
</head>
<body>
    <div class='container mt-5'>
        <div class='card mt-4'>
            <div class='card-body'>
                <h5 class='card-title'>Укажите frontend для правильной обработки запроса</h5>
				<br>
                <p class='card-text'>Добавьте в init.conf следующий код:</p>
                <pre style='background: #e9ecef;'><code>""listen"": {
  ""frontend"": ""cloudflare""
}</code></pre>
				<br>
                <p class='card-text'>Либо отключите проверку CF-Connecting-IP:</p>
                <pre style='background: #e9ecef;'><code>""listen"": {
  ""frontend"": ""off""
}</code></pre>
				<br>
                <p class='card-text'>Так же параметр можно изменить в <a href='/admin' target='_blank'>админке</a>: Остальное, base, frontend</p>
            </div>
        </div>
    </div>
</body>
</html>";
    }
}
