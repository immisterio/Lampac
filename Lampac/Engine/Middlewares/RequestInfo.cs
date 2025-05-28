﻿using Lampac.Engine.CORE;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine;
using Shared.Models;
using System;
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
            {
                string skey = $"stats:request:{DateTime.Now.Minute}";
                if (!memoryCache.TryGetValue(skey, out long _req))
                    _req = 0;

                _req++;
                memoryCache.Set(skey, _req, DateTime.Now.AddMinutes(58));
            }
            #endregion

            bool IsLocalRequest = false;
            string clientIp = httpContext.Connection.RemoteIpAddress.ToString();

            if (httpContext.Request.Headers.TryGetValue("localrequest", out var _localpasswd))
            {
                if (_localpasswd.ToString() != FileCache.ReadAllText("passwd"))
                    return httpContext.Response.WriteAsync("error passwd", httpContext.RequestAborted);

                IsLocalRequest = true;

                if (httpContext.Request.Headers.TryGetValue("x-client-ip", out var xip) && !string.IsNullOrEmpty(xip))
                    clientIp = xip;
            }
            else if (AppInit.conf.real_ip_cf || AppInit.conf.frontend == "cloudflare")
            {
                #region cloudflare
                if (Program.cloudflare_ips != null && Program.cloudflare_ips.Count > 0)
                {
                    var clientIPAddress = IPAddress.Parse(clientIp);
                    foreach (var cf in Program.cloudflare_ips)
                    {
                        if (new IPNetwork(cf.prefix, cf.prefixLength).Contains(clientIPAddress))
                        {
                            if (httpContext.Request.Headers.TryGetValue("CF-Connecting-IP", out var xip) && !string.IsNullOrEmpty(xip))
                                clientIp = xip;

                            try
                            {
                                if (httpContext.Request.Headers.TryGetValue("CF-Visitor", out var cfVisitor))
                                {
                                    var visitorInfo = JsonNode.Parse(cfVisitor);
                                    if (visitorInfo != null && visitorInfo["scheme"] != null)
                                        httpContext.Request.Scheme = visitorInfo["scheme"].ToString();
                                }
                            }
                            catch { }

                            break;
                        }
                    }
                }
                #endregion
            }
            else if (httpContext.Request.Headers.ContainsKey("CF-Connecting-IP"))
            {
                if (string.IsNullOrEmpty(AppInit.conf.frontend))
                    return httpContext.Response.WriteAsync(unknownFrontend, httpContext.RequestAborted);
            }

            var req = new RequestModel()
            {
                IsLocalRequest = IsLocalRequest,
                IP = clientIp,
                Path = httpContext.Request.Path.Value,
                Query = httpContext.Request.QueryString.Value,
                UserAgent = httpContext.Request.Headers.UserAgent
            };

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
                return _next(httpContext);
            }
            else
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
        }


        static string unknownFrontend = @"<!DOCTYPE html>
<html lang='ru'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>CloudFlare</title>
    <link href='https://stackpath.bootstrapcdn.com/bootstrap/4.5.2/css/bootstrap.min.css' rel='stylesheet'>
</head>
<body>
    <div class='container mt-5'>
        <div class='card mt-4'>
            <div class='card-body'>
                <h5 class='card-title'>Укажите frontend для правильной обработки запроса</h5>
				<br>
                <p class='card-text'>Добавьте в init.conf следующий код:</p>
                <pre style='background: #e9ecef;'><code>""frontend"": ""cloudflare""</code></pre>
				<br>
                <p class='card-text'>Либо отключите проверку CF-Connecting-IP:</p>
                <pre style='background: #e9ecef;'><code>""frontend"": ""off""</code></pre>
				<br>
                <p class='card-text'>Так же параметр можно изменить в админке: Остальное, base, frontend</p>
            </div>
        </div>
    </div>
</body>
</html>";
    }
}
