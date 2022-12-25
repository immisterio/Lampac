using Microsoft.AspNetCore.Http;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Lampac.Engine.Middlewares
{
    public class Accsdb
    {
        private readonly RequestDelegate _next;
        public Accsdb(RequestDelegate next)
        {
            _next = next;
        }

        public Task Invoke(HttpContext httpContext)
        {
            string jacpattern = "^/(api/v2.0/indexers|api/v1.0/|toloka|rutracker|rutor|torrentby|nnmclub|kinozal|bitru|selezen|megapeer|animelayer|anilibria|toloka)";

            if (!string.IsNullOrWhiteSpace(AppInit.conf.jac.apikey))
            {
                if (Regex.IsMatch(httpContext.Request.Path.Value, jacpattern))
                {
                    if (AppInit.conf.jac.apikey != Regex.Match(httpContext.Request.QueryString.Value, "(\\?|&)apikey=([^&]+)").Groups[2].Value)
                        return Task.CompletedTask;
                }
            }

            if (AppInit.conf.accsdb.enable)
            {
                if (httpContext.Request.Path.Value != "/" && httpContext.Connection.RemoteIpAddress.ToString() != "127.0.0.1" &&
                    !Regex.IsMatch(httpContext.Request.Path.Value, jacpattern) && 
                    !Regex.IsMatch(httpContext.Request.Path.Value, "^/(sisi|[a-zA-Z]+\\.js|msx/start\\.json|samsung\\.wgt)"))
                {
                    string account_email = HttpUtility.UrlDecode(Regex.Match(httpContext.Request.QueryString.Value, "(\\?|&)account_email=([^&]+)").Groups[2].Value);
                    string msg = string.IsNullOrWhiteSpace(account_email) ? AppInit.conf.accsdb.cubMesage : AppInit.conf.accsdb.denyMesage.Replace("{account_email}", account_email);

                    if (string.IsNullOrWhiteSpace(account_email) || !AppInit.conf.accsdb.accounts.Contains(account_email))
                    {
                        httpContext.Response.ContentType = "application/javascript; charset=utf-8";
                        return httpContext.Response.WriteAsync("{{\"accsdb\":true,\"msg\":\"" + msg + "\"}}");
                    }
                }
            }

            return _next(httpContext);
        }
    }
}
