﻿using Microsoft.AspNetCore.Http;
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
            var req = new RequestModel()
            {
                IP = httpContext.Connection.RemoteIpAddress.ToString(),
                UserAgent = httpContext.Request.Headers.UserAgent,
                Country = GeoIP2.Country(httpContext.Connection.RemoteIpAddress.ToString())
            };

            if (string.IsNullOrEmpty(AppInit.conf.accsdb.domainId_pattern))
            {
                req.user = AppInit.conf.accsdb.findUser(httpContext, out string uid);
                req.user_uid = uid;

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