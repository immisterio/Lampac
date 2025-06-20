using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;

namespace Shared.Engine
{
    public static class ResponseCache
    {
        public static string ErrorKey(HttpContext httpContext)
        {
            string key = httpContext.Request.Path.Value + httpContext.Request.QueryString.Value;
            return "ResponseCache:errorMsg:" + Regex.Replace(key, "(\\?|&)(account_email|cub_id|box_mac|uid|token|source|rchtype)=[^&]+", "");
        }
    }
}
