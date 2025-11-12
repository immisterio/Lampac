using Microsoft.AspNetCore.Http;
using System.Text;
using System.Web;

namespace Shared.Engine
{
    public static class AccsDbInvk
    {
        public static string Args(string uri, HttpContext httpContext)
        {
            var args = new StringBuilder();

            if (httpContext.Request.Query.ContainsKey("account_email") && !uri.Contains("account_email="))
            {
                string account_email = httpContext.Request.Query["account_email"].ToString()?.ToLower()?.Trim();
                if (!string.IsNullOrEmpty(account_email))
                    args.Append($"&account_email={HttpUtility.UrlEncode(account_email)}");
            }

            if (httpContext.Request.Query.ContainsKey("uid") && !uri.Contains("uid="))
            {
                string uid = httpContext.Request.Query["uid"].ToString()?.ToLower()?.Trim();
                if (!string.IsNullOrEmpty(uid))
                    args.Append($"&uid={HttpUtility.UrlEncode(uid)}");
            }

            if (httpContext.Request.Query.ContainsKey("token") && !uri.Contains("token="))
            {
                string token = httpContext.Request.Query["token"].ToString()?.ToLower()?.Trim();
                if (!string.IsNullOrEmpty(token))
                    args.Append($"&token={HttpUtility.UrlEncode(token)}");
            }

            if (httpContext.Request.Query.ContainsKey("box_mac") && !uri.Contains("box_mac="))
            {
                string box_mac = httpContext.Request.Query["box_mac"].ToString()?.ToLower()?.Trim();
                if (!string.IsNullOrEmpty(box_mac))
                    args.Append($"&box_mac={HttpUtility.UrlEncode(box_mac)}");
            }

            if (httpContext.Request.Query.ContainsKey("nws_id") && !uri.Contains("nws_id="))
            {
                string nws_id = httpContext.Request.Query["nws_id"].ToString()?.ToLower()?.Trim();
                if (!string.IsNullOrEmpty(nws_id))
                    args.Append($"&nws_id={HttpUtility.UrlEncode(nws_id)}");
            }

            if (args.Length == 0)
                return uri;

            if (string.IsNullOrEmpty(uri))
                return args.ToString();

            if (uri.Contains("?"))
                return uri + args.ToString();

            return $"{uri}?{args.Remove(0, 1).ToString()}";
        }
    }
}
