using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Runtime.CompilerServices;
using System.Text;

namespace Shared.Services.Utilities;

public static class AccsDbInvk
{
    public static string Args(string uri, HttpContext httpContext)
    {
        var args = StringBuilderPool.ThreadInstance;

        args.Append(uri);
        int initialLength = args.Length;
        char split = uri.Contains('?') ? '&' : '?';

        bool hasAccountEmail = false;
        bool hasUid = false;
        bool hasToken = false;
        bool hasBoxMac = false;
        bool hasNws = false;

        var query = httpContext.Request.Query;
        query.TryGetValue("account_email", out StringValues account_email);
        query.TryGetValue("uid", out StringValues uid);
        query.TryGetValue("token", out StringValues token);
        query.TryGetValue("box_mac", out StringValues box_mac);
        query.TryGetValue("nws_id", out StringValues nws_id);

        int indexArgs = uri.IndexOf('?');
        if (indexArgs > 0)
        {
            ReadOnlySpan<char> _u = uri.AsSpan(indexArgs);

            if (account_email.Count > 0)
                hasAccountEmail = HasQueryParamWithNonEmptyValue(_u, "account_email=");

            if (uid.Count > 0)
                hasUid = HasQueryParamWithNonEmptyValue(_u, "uid=");

            if (token.Count > 0)
                hasToken = HasQueryParamWithNonEmptyValue(_u, "token=");

            if (box_mac.Count > 0)
                hasBoxMac = HasQueryParamWithNonEmptyValue(_u, "box_mac=");

            if (nws_id.Count > 0)
                hasNws = HasQueryParamWithNonEmptyValue(_u, "nws_id=");
        }

        if (!hasAccountEmail)
            AppendParam(ref split, args, "account_email", account_email);

        if (!hasUid)
            AppendParam(ref split, args, "uid", uid);

        if (!hasToken)
            AppendParam(ref split, args, "token", token);

        if (!hasBoxMac)
            AppendParam(ref split, args, "box_mac", box_mac);

        if (!hasNws)
            AppendParam(ref split, args, "nws_id", nws_id);

        if (args.Length == initialLength)
            return uri;

        return args.ToString();
    }

    static bool HasQueryParamWithNonEmptyValue(ReadOnlySpan<char> uri, ReadOnlySpan<char> key)
    {
        if (uri.IsEmpty || key.IsEmpty)
            return false;

        int searchFrom = 0;

        while (searchFrom < uri.Length)
        {
            int pos = uri[searchFrom..]
                .IndexOf(key, StringComparison.OrdinalIgnoreCase);

            if (pos < 0)
                return false;

            pos += searchFrom;

            // key должен начинаться либо с 0, либо после '?' или '&'
            if (pos != 0)
            {
                char prev = uri[pos - 1];
                if (prev != '?' && prev != '&')
                {
                    searchFrom = pos + key.Length;
                    continue;
                }
            }

            int afterKey = pos + key.Length;

            // после '=' должен быть хотя бы 1 символ
            if (afterKey >= uri.Length)
            {
                searchFrom = afterKey;
                continue;
            }

            char firstValueChar = uri[afterKey];

            // значение не должно начинаться с разделителя или fragment
            if (firstValueChar == '&' || firstValueChar == '#')
            {
                searchFrom = afterKey + 1;
                continue;
            }

            return true;
        }

        return false;
    }

    static void AppendParam(ref char split, StringBuilder sb, string key, in StringValues values)
    {
        if (values.Count == 0)
            return;

        string value = values[0];
        if (string.IsNullOrEmpty(value))
            return;

        sb.Append(split);
        if (split == '?')
            split = '&';

        sb.Append(key);
        sb.Append('=');

        if (CoreInit.conf.BaseModule.ValidateIdentity)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char ch = char.ToLowerInvariant(value[i]);

                if (IsAllowedChar(ch))
                {
                    sb.Append(ch);
                    continue;
                }
            }
        }
        else
            sb.Append(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAllowedChar(char c)
        => (c >= 'a' && c <= 'z')
        || (c >= '0' && c <= '9')
        || c == '@' || c == '.' || c == '-' || c == '_' || c == '=';
}
