using Lampac.Engine.CORE;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine;
using Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class Accsdb
    {
        static Accsdb() 
        {
            Directory.CreateDirectory("cache/logs/accsdb");
        }

        private readonly RequestDelegate _next;
        IMemoryCache memoryCache;

        public Accsdb(RequestDelegate next, IMemoryCache mem)
        {
            _next = next;
            memoryCache = mem;
        }

        public Task Invoke(HttpContext httpContext)
        {
            var requestInfo = httpContext.Features.Get<RequestModel>();
            if (requestInfo.IsLocalRequest)
                return _next(httpContext);

            #region manifest / admin
            if (!File.Exists("module/manifest.json"))
            {
                if (httpContext.Request.Path.Value.StartsWith("/admin/manifest/install"))
                    return _next(httpContext);

                httpContext.Response.Redirect("/admin/manifest/install");
                return Task.CompletedTask;
            }

            if (httpContext.Request.Path.Value.StartsWith("/admin/"))
            {
                if (httpContext.Request.Path.Value.StartsWith("/admin/auth"))
                    return _next(httpContext);

                if (httpContext.Request.Cookies.TryGetValue("passwd", out string passwd) && passwd == FileCache.ReadAllText("passwd"))
                    return _next(httpContext);

                httpContext.Response.Redirect("/admin/auth");
                return Task.CompletedTask;
            }
            #endregion

            if (httpContext.Request.Path.Value.StartsWith("/ws"))
            {
                if (!AppInit.conf.weblog.enable && !AppInit.conf.rch.enable && !AppInit.conf.storage.enable)
                    return httpContext.Response.WriteAsync("disabled", httpContext.RequestAborted);
            }

            string jacpattern = "^/(api/v2.0/indexers|api/v1.0/|toloka|rutracker|rutor|torrentby|nnmclub|kinozal|bitru|selezen|megapeer|animelayer|anilibria|anifilm|toloka|lostfilm|baibako|hdrezka)";

            if (!string.IsNullOrEmpty(AppInit.conf.apikey))
            {
                if (Regex.IsMatch(httpContext.Request.Path.Value, jacpattern))
                {
                    if (AppInit.conf.apikey != httpContext.Request.Query["apikey"])
                        return Task.CompletedTask;
                }
            }

            if (AppInit.conf.accsdb.enable)
            {
                if (!string.IsNullOrEmpty(AppInit.conf.accsdb.premium_pattern) && !Regex.IsMatch(httpContext.Request.Path.Value, AppInit.conf.accsdb.premium_pattern, RegexOptions.IgnoreCase))
                    return _next(httpContext);

                if (!string.IsNullOrEmpty(AppInit.conf.accsdb.whitepattern) && Regex.IsMatch(httpContext.Request.Path.Value, AppInit.conf.accsdb.whitepattern, RegexOptions.IgnoreCase))
                    return _next(httpContext);

                if (Regex.IsMatch(httpContext.Request.Path.Value, jacpattern))
                    return _next(httpContext);

                if (httpContext.Request.Path.Value.EndsWith("/personal.lampa"))
                    return _next(httpContext);

                if (httpContext.Request.Path.Value != "/" && !Regex.IsMatch(httpContext.Request.Path.Value, "^/((proxy-dash|ts|ws|headers|myip|geo|version|weblog|rch/result|merchant/payconfirm|bind)(/|$)|cub/(api/checker|[^/]+/(api/checker|api/plugins/|plugin/))|(extensions|kit)$|on/|(lite|online|sisi|timecode|sync|tmdbproxy|dlna|ts|tracks|backup|invc-ws)/js/|(streampay|b2pay|cryptocloud|freekassa|litecoin)/|lite/(filmixpro|fxapi/lowlevel/|kinopubpro|vokinotk|rhs/bind)|([^/]+/)?app\\.min\\.js|css/app\\.css|[a-zA-Z\\-]+\\.js|msx/start\\.json|samsung\\.wgt)"))
                {
                    bool limitip = false;

                    var user = requestInfo.user;
                    string uri = httpContext.Request.Path.Value+httpContext.Request.QueryString.Value;

                    if (user == null || user.ban || DateTime.UtcNow > user.expires || IsLockHostOrUser(requestInfo.user_uid, requestInfo.IP, uri, out limitip))
                    {
                        if (Regex.IsMatch(httpContext.Request.Path.Value, "^/(proxy/|proxyimg)"))
                        {
                            string href = Regex.Replace(httpContext.Request.Path.Value, "^/(proxy|proxyimg([^/]+)?)/", "") + httpContext.Request.QueryString.Value;

                            if (href.Contains(".themoviedb.org") || href.Contains(".tmdb.org") || href.StartsWith("http"))
                            {
                                httpContext.Response.Redirect(href);
                                return Task.CompletedTask;
                            }
                        }

                        if (Regex.IsMatch(httpContext.Request.Path.Value, "\\.(js|css|ico|png|svg|jpe?g|woff|webmanifest)"))
                        {
                            httpContext.Response.StatusCode = 404;
                            httpContext.Response.ContentType = "application/octet-stream";
                            return Task.CompletedTask;
                        }

                        string msg = (user != null && user.ban) ? (user.ban_msg ?? "Вы заблокированы") : 
                                     limitip ? $"Превышено допустимое количество ip/запросов на аккаунт." : // Разбан через {60 - DateTime.Now.Minute} мин.\n{string.Join(", ", ips)}
                                     string.IsNullOrWhiteSpace(requestInfo.user_uid) ? AppInit.conf.accsdb.authMesage :
                                     user != null ? AppInit.conf.accsdb.expiresMesage.Replace("{account_email}", requestInfo.user_uid).Replace("{expires}", user.expires.ToString("dd.MM.yyyy")) :
                                     AppInit.conf.accsdb.denyMesage.Replace("{account_email}", requestInfo.user_uid);

                        httpContext.Response.ContentType = "application/javascript; charset=utf-8";
                        return httpContext.Response.WriteAsync("{\"accsdb\":true,\"msg\":\"" + msg + "\"}", httpContext.RequestAborted);
                    }
                }
            }

            return _next(httpContext);
        }


        #region IsLock
        static string logsLock = string.Empty;

        bool IsLockHostOrUser(string account_email, string userip, string uri, out bool islock)
        {
            if (string.IsNullOrEmpty(account_email))
            {
                islock = false;
                return islock;
            }

            HashSet<string> ips = null;
            HashSet<string> urls = null;

            #region setLogs
            void setLogs(string name)
            {
                string logFile = $"cache/logs/accsdb/{DateTime.Now:dd-MM-yyyy}.lock.txt";
                if (logsLock != string.Empty && !File.Exists(logFile))
                    logsLock = string.Empty;

                string line = $"{name} / {account_email} / {CrypTo.md5(account_email)}.*.log";

                if (!logsLock.Contains(line))
                {
                    logsLock += $"{DateTime.Now}: {line}\n";
                    File.WriteAllText(logFile, logsLock);
                }
            }
            #endregion

            #region countlock_day
            int countlock_day(bool update)
            {
                string key = $"Accsdb:lock_day:{account_email}:{DateTime.Now.Day}";

                if (memoryCache.TryGetValue(key, out HashSet<int> lockhour))
                {
                    if (update)
                    {
                        lockhour.Add(DateTime.Now.Hour);
                        memoryCache.Set(key, lockhour, DateTime.Now.AddDays(1));
                    }

                    return lockhour.Count;
                }
                else if (update)
                {
                    lockhour = new HashSet<int>() { DateTime.Now.Hour };
                    memoryCache.Set(key, lockhour, DateTime.Now.AddDays(1));
                    return lockhour.Count;
                }

                return 0;
            }
            #endregion

            if (IsLockIpHour(account_email, userip, out islock, out ips) || IsLockReqHour(account_email, uri, out islock, out urls))
            {
                setLogs("lock_hour");
                countlock_day(update: true);

                File.WriteAllLines($"cache/logs/accsdb/{CrypTo.md5(account_email)}.ips.log", ips);
                File.WriteAllLines($"cache/logs/accsdb/{CrypTo.md5(account_email)}.urls.log", urls);

                return islock;
            }

            if (countlock_day(update: false) > AppInit.conf.accsdb.maxlock_day)
            {
                if (AppInit.conf.accsdb.blocked_hour != -1)
                    memoryCache.Set($"Accsdb:blocked_hour:{account_email}", 0, DateTime.Now.AddHours(AppInit.conf.accsdb.blocked_hour));

                setLogs("lock_day");
                islock = true;
                return islock;
            }

            if (memoryCache.TryGetValue($"Accsdb:blocked_hour:{account_email}", out _))
            {
                setLogs("blocked");
                islock = true;
                return islock;
            }

            islock = false;
            return islock;
        }


        bool IsLockIpHour(string account_email, string userip, out bool islock, out HashSet<string> ips)
        {
            string memKeyLocIP = $"Accsdb:IsLockIpHour:{account_email}:{DateTime.Now.Hour}";

            if (memoryCache.TryGetValue(memKeyLocIP, out ips))
            {
                ips.Add(userip);
                memoryCache.Set(memKeyLocIP, ips, DateTime.Now.AddHours(1));

                if (ips.Count > AppInit.conf.accsdb.maxip_hour)
                {
                    islock = true;
                    return islock;
                }
            }
            else
            {
                ips = new HashSet<string>() { userip };
                memoryCache.Set(memKeyLocIP, ips, DateTime.Now.AddHours(1));
            }

            islock = false;
            return islock;
        }

        bool IsLockReqHour(string account_email, string uri, out bool islock, out HashSet<string> urls)
        {
            if (Regex.IsMatch(uri, "^/(proxy/|proxyimg|lifeevents|externalids|ts/|storage/|tmdb/|timecode)"))
            {
                urls = new HashSet<string>();
                islock = false;
                return islock;
            }

            string memKeyLocIP = $"Accsdb:IsLockReqHour:{account_email}:{DateTime.Now.Hour}";

            if (memoryCache.TryGetValue(memKeyLocIP, out urls))
            {
                urls.Add(uri);
                memoryCache.Set(memKeyLocIP, urls, DateTime.Now.AddHours(1));

                if (urls.Count > AppInit.conf.accsdb.maxrequest_hour)
                {
                    islock = true;
                    return islock;
                }
            }
            else
            {
                urls = new HashSet<string>() { uri };
                memoryCache.Set(memKeyLocIP, urls, DateTime.Now.AddHours(1));
            }

            islock = false;
            return islock;
        }
        #endregion
    }
}
