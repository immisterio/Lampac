using Microsoft.AspNetCore.Http;
using System.Web;

namespace Shared.Engine.CORE
{
    public static class AccsDbInvk
    {
        public static string Args(string uri, HttpContext httpContext)
        {
            string args = string.Empty;

            string account_email = httpContext.Request.Query["account_email"].ToString()?.ToLower()?.Trim();
            if (!string.IsNullOrEmpty(account_email) && !uri.Contains("account_email="))
                args += $"&account_email={HttpUtility.UrlEncode(account_email)}";

            string uid = httpContext.Request.Query["uid"].ToString()?.ToLower()?.Trim();
            if (!string.IsNullOrEmpty(uid) && !uri.Contains("uid="))
                args += $"&uid={HttpUtility.UrlEncode(uid)}";

            string token = httpContext.Request.Query["token"].ToString()?.ToLower()?.Trim();
            if (!string.IsNullOrEmpty(token) && !uri.Contains("token="))
                args += $"&token={HttpUtility.UrlEncode(token)}";

            string box_mac = httpContext.Request.Query["box_mac"].ToString()?.ToLower()?.Trim();
            if (!string.IsNullOrEmpty(box_mac) && !uri.Contains("box_mac="))
                args += $"&box_mac={HttpUtility.UrlEncode(box_mac)}";

            if (args == string.Empty)
                return uri;

            if (uri.Contains("?"))
                return uri + args;

            return $"{uri}?{args.Remove(0, 1)}";
        }
    }
}
