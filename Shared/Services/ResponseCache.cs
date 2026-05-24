using Microsoft.AspNetCore.Http;

namespace Shared.Services;

public static class ResponseCache
{
    const string Prefix = "ResponseCache:errorMsg:";

    public static string ErrorKey(HttpContext httpContext)
    {
        if (string.IsNullOrEmpty(httpContext.Request.Path.Value) ||
            string.IsNullOrEmpty(httpContext.Request.QueryString.Value))
            return null;

        var hash = Fnv1a.Empty;

        Fnv1a.Append(ref hash, Prefix);
        Fnv1a.Append(ref hash, httpContext.Request.Path.Value);

        foreach (var kvp in httpContext.Request.Query)
        {
            if (kvp.Key == "source")
                continue;

            if (CoreInit.SkipQueryKeys.Contains(kvp.Key))
                continue;

            foreach (var value in kvp.Value)
            {
                Fnv1a.Append(ref hash, kvp.Key);
                Fnv1a.Append(ref hash, value);
            }
        }

        return Fnv1a.Base64Url(hash);
    }
}
