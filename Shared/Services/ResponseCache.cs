using Microsoft.AspNetCore.Http;
using System.Collections.Frozen;

namespace Shared.Services;

public static class ResponseCache
{
    static readonly FrozenSet<string> SensitiveKeys = new[]
    {
        "account_email", "cub_id", "box_mac", "uid", "token", "source", "rchtype", "nws_id"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    const string Prefix = "ResponseCache:errorMsg:";


    public static string ErrorKey(HttpContext httpContext)
    {
        if (string.IsNullOrEmpty(httpContext.Request.Path.Value) ||
            string.IsNullOrEmpty(httpContext.Request.QueryString.Value))
            return null;

        var sb = StringBuilderPool.ThreadInstance;

        sb.Append(Prefix);
        sb.Append(httpContext.Request.Path.Value);

        bool first = true;
        foreach (var kvp in httpContext.Request.Query)
        {
            if (SensitiveKeys.Contains(kvp.Key))
                continue;

            foreach (var value in kvp.Value)
            {
                sb.Append(first ? '?' : '&');
                first = false;

                sb.Append(kvp.Key);
                sb.Append('=');
                sb.Append(value);
            }
        }

        return sb.ToString();
    }
}
