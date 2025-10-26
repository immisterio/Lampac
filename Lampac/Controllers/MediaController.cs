using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Shared;
using Shared.Engine;
using Shared.Models;
using Shared.Models.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Lampac.Controllers
{
    public class MediaController : BaseController
    {
        #region Routes
        [HttpGet]
        [Route("/media/rsize/{token}/{width}/{height}/{*url}")]
        public ActionResult Get(string token, int width, int height, string url)
        {
            return GetLocation(url + HttpContext.Request.QueryString.Value, new MediaRequestBase
            {
                type = "img",
                auth_token = token,
                width = width,
                height = height
            }, null, null);
        }

        [HttpGet]
        [Route("/media/{type}/{token}/{*url}")]
        public ActionResult Get(string type, string token, string url)
        {
            return GetLocation(url + HttpContext.Request.QueryString.Value, new MediaRequestBase
            {
                auth_token = token,
                type = type
            }, null, null);
        }

        [HttpGet]
        [Route("/media")]
        public ActionResult Get(string url, string headers, [FromQuery] MediaRequestBase request)
        {
            var webProxy = CreateProxy(request?.proxy, request?.proxy_name);
            var headerList = HeadersModel.Init(ParseHeaders(headers));

            return GetLocation(url, request, headerList, webProxy);
        }

        [HttpPost]
        [Route("/media")]
        public ActionResult Post([FromBody] MediaRequest request)
        {
            if (!TryValidateBase(request, out ActionResult errorResult))
                return errorResult;

            if (request.urls == null || request.urls.Count == 0)
                return JsonError("invalid urls", 400);

            var webProxy = CreateProxy(request.proxy, request.proxy_name);
            var headerList = HeadersModel.Init(request.headers);
            var streamSettings = CreateStreamSettings(request);

            var result = new List<string>(request.urls.Count);

            foreach (string source in request.urls)
            {
                string proxied = request.type == "img"
                    ? CreateImageProxy(source, request.width, request.height, headerList, webProxy)
                    : HostStreamProxy(streamSettings, source, headerList, webProxy);

                result.Add(proxied);
            }

            return Json(new
            {
                success = true,
                urls = result
            });
        }
        #endregion

        #region Helpers
        ActionResult GetLocation(string url, MediaRequestBase request, List<HeadersModel> headers, WebProxy proxy)
        {
            if (string.IsNullOrEmpty(url))
                return JsonError("invalid url", 400);

            if (!TryValidateBase(request, out ActionResult errorResult))
                return errorResult;

            string location = request.type == "img"
                ? CreateImageProxy(url, request.width, request.height, headers, proxy)
                : HostStreamProxy(CreateStreamSettings(request), url, headers, proxy);

            return Redirect(location);
        }

        BaseSettings CreateStreamSettings(MediaRequestBase request)
        {
            return new BaseSettings
            {
                plugin = "media",
                streamproxy = true,
                apnstream = request.apnstream,
                useproxystream = request.useproxystream
            };
        }

        bool TryValidateBase(MediaRequestBase request, out ActionResult errorResult)
        {
            errorResult = null;
            var init = AppInit.conf.media;

            if (request == null)
            {
                errorResult = JsonError("invalid request", 400);
                return false;
            }

            if (string.IsNullOrEmpty(request.auth_token) || init?.tokens == null || !init.tokens.Any(t => t == request.auth_token))
            {
                errorResult = JsonError("unauthorized", 401);
                return false;
            }

            return true;
        }

        Dictionary<string, string> ParseHeaders(string headers)
        {
            try
            {
                if (!string.IsNullOrEmpty(headers))
                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(headers);
            }
            catch { }

            return null;
        }

        WebProxy CreateProxy(string proxyValue, string proxyName)
        {
            ProxySettings proxySettings = null;

            if (!string.IsNullOrEmpty(proxyValue))
            {
                proxySettings = new ProxySettings
                {
                    list = [proxyValue]
                };
            }
            else if (!string.IsNullOrEmpty(proxyName) && AppInit.conf.globalproxy != null)
            {
                var settings = AppInit.conf.globalproxy.FirstOrDefault(i => i.name == proxyName);
                if (settings?.list != null && settings.list.Length > 0)
                    proxySettings = settings;
            }

            if (proxySettings == null)
                return null;

            return ProxyManager.ConfigureWebProxy(proxySettings, proxySettings.list.First()).proxy;
        }

        string CreateImageProxy(string url, int? width, int? height, List<HeadersModel> headers, WebProxy proxy)
        {
            if (!AppInit.conf.serverproxy.enable)
                return url;

            string encrypted = ProxyLink.Encrypt(url, requestInfo.IP, headers, proxy, "posterapi", verifyip: false);

            if (AppInit.conf.accsdb.enable && !AppInit.conf.serverproxy.encrypt)
                encrypted = AccsDbInvk.Args(encrypted, HttpContext);

            int normalizedWidth = Math.Max(0, width ?? 0);
            int normalizedHeight = Math.Max(0, height ?? 0);

            if (normalizedWidth > 0 || normalizedHeight > 0)
                return $"{host}/proxyimg:{normalizedWidth}:{normalizedHeight}/{encrypted}";

            return $"{host}/proxyimg/{encrypted}";
        }

        ActionResult JsonError(string message, int statusCode)
        {
            HttpContext.Response.StatusCode = statusCode;
            return ContentTo(JsonConvert.SerializeObject(new
            {
                success = false,
                error = message
            }));
        }
        #endregion
    }
}