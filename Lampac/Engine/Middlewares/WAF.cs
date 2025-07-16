﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Caching.Memory;
using Shared.Models;
using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class WAF
    {
        IMemoryCache memoryCache;
        private readonly RequestDelegate _next;
        public WAF(RequestDelegate next, IMemoryCache mem)
        {
            _next = next;
            memoryCache = mem;
        }

        public Task Invoke(HttpContext httpContext)
        {
            var waf = AppInit.conf.WAF;
            if (!waf.enable)
                return _next(httpContext);

            var requestInfo = httpContext.Features.Get<RequestModel>();
            if (requestInfo.IsLocalRequest)
                return _next(httpContext);

            #region country
            if (waf.countryAllow != null)
            {
                // если мы не знаем страну или точно знаем, что она не в списке разрешенных
                if (string.IsNullOrEmpty(requestInfo.Country) || !waf.countryAllow.Contains(requestInfo.Country))
                {
                    httpContext.Response.StatusCode = 403;
                    return Task.CompletedTask;
                }
            }

            if (waf.countryDeny != null)
            {
                // точно знаем страну и она есть в списке запрещенных
                if (!string.IsNullOrEmpty(requestInfo.Country) && waf.countryDeny.Contains(requestInfo.Country))
                {
                    httpContext.Response.StatusCode = 403;
                    return Task.CompletedTask;
                }
            }
            #endregion

            #region ips
            if (waf.ipsDeny != null)
            {
                if (waf.ipsDeny.Contains(requestInfo.IP))
                {
                    httpContext.Response.StatusCode = 403;
                    return Task.CompletedTask;
                }

                var clientIPAddress = IPAddress.Parse(requestInfo.IP);
                foreach (string ip in waf.ipsDeny)
                {
                    if (ip.Contains("/"))
                    {
                        string[] parts = ip.Split('/');
                        if (int.TryParse(parts[1], out int prefixLength))
                        {
                            if (new IPNetwork(IPAddress.Parse(parts[0]), prefixLength).Contains(clientIPAddress))
                            {
                                httpContext.Response.StatusCode = 403;
                                return Task.CompletedTask;
                            }
                        }
                    }
                }
            }

            if (waf.ipsAllow != null)
            {
                if (!waf.ipsAllow.Contains(requestInfo.IP))
                {
                    bool deny = true;
                    var clientIPAddress = IPAddress.Parse(requestInfo.IP);
                    foreach (string ip in waf.ipsAllow)
                    {
                        if (ip.Contains("/"))
                        {
                            string[] parts = ip.Split('/');
                            if (int.TryParse(parts[1], out int prefixLength))
                            {
                                if (new IPNetwork(IPAddress.Parse(parts[0]), prefixLength).Contains(clientIPAddress))
                                {
                                    deny = false;
                                    break;
                                }
                            }
                        }
                    }

                    if (deny)
                    {
                        httpContext.Response.StatusCode = 403;
                        return Task.CompletedTask;
                    }
                }
            }
            #endregion

            #region headers
            if (waf.headersDeny != null)
            {
                foreach (var header in waf.headersDeny)
                {
                    if (httpContext.Request.Headers.TryGetValue(header.Key, out var headerValue) && !string.IsNullOrEmpty(headerValue))
                    {
                        if (Regex.IsMatch(headerValue.ToString(), header.Value, RegexOptions.IgnoreCase))
                        {
                            httpContext.Response.StatusCode = 403;
                            return Task.CompletedTask;
                        }
                    }
                }
            }
            #endregion

            #region limit_req
            if (waf.limit_req > 0)
            {
                if (RateLimited(requestInfo.IP, waf.limit_req))
                {
                    httpContext.Response.StatusCode = 429;
                    return Task.CompletedTask;
                }
            }
            #endregion

            return _next(httpContext);
        }


        bool RateLimited(string userip, int limit_req)
        {
            string memKeyLocIP = $"WAF:RateLimited:{userip}:{DateTime.Now.Minute}";

            if (memoryCache.TryGetValue(memKeyLocIP, out int req))
            {
                if (req >= limit_req)
                    return true;

                memoryCache.Set(memKeyLocIP, req+1, DateTime.Now.AddMinutes(1));
            }
            else
            {
                memoryCache.Set(memKeyLocIP, 1, DateTime.Now.AddMinutes(1));
            }

            return false;
        }
    }
}
