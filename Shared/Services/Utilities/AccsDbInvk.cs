using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Buffers;
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

        if (httpContext.Request.Query.ContainsKey("account_email") && !uri.Contains("account_email=", StringComparison.OrdinalIgnoreCase))
            AppendParam(ref split, args, "account_email", httpContext.Request.Query["account_email"]);

        if (httpContext.Request.Query.ContainsKey("uid") && !uri.Contains("uid=", StringComparison.OrdinalIgnoreCase))
            AppendParam(ref split, args, "uid", httpContext.Request.Query["uid"]);

        if (httpContext.Request.Query.ContainsKey("token") && !uri.Contains("token=", StringComparison.OrdinalIgnoreCase))
            AppendParam(ref split, args, "token", httpContext.Request.Query["token"]);

        if (httpContext.Request.Query.ContainsKey("box_mac") && !uri.Contains("box_mac=", StringComparison.OrdinalIgnoreCase))
            AppendParam(ref split, args, "box_mac", httpContext.Request.Query["box_mac"]);

        if (httpContext.Request.Query.ContainsKey("nws_id") && !uri.Contains("nws_id=", StringComparison.OrdinalIgnoreCase))
            AppendParam(ref split, args, "nws_id", httpContext.Request.Query["nws_id"]);

        if (args.Length == initialLength)
            return uri;

        return args.ToString();
    }


    static void AppendParam(ref char split, StringBuilder sb, string key, in StringValues values)
    {
        if (values.Count == 0)
            return;

        string s = values[0];
        if (string.IsNullOrEmpty(s))
            return;

        sb.Append(split);
        if (split == '?')
            split = '&';

        sb.Append(key);
        sb.Append('=');

        if (CoreInit.conf.BaseModule.ValidateIdentity)
            AppendUrlEncodedLowerInvariant(sb, s.AsSpan().Trim());
        else
            sb.Append(s);
    }

    static void AppendUrlEncodedLowerInvariant(StringBuilder sb, ReadOnlySpan<char> value)
    {
        Span<byte> utf8 = stackalloc byte[4]; // максимум 4 байта на один Rune (UTF-8)

        for (int i = 0; i < value.Length; i++)
        {
            char ch = char.ToLowerInvariant(value[i]);

            if (!IsAllowedChar(ch))
                continue;

            if (IsUnreserved(ch))
            {
                sb.Append(ch);
                continue;
            }

            if (ch == ' ')
            {
                sb.Append("%20");
                continue;
            }

            // Корректно декодируем один Unicode scalar (1 или 2 char UTF-16)
            ReadOnlySpan<char> tail = value.Slice(i);
            OperationStatus status = Rune.DecodeFromUtf16(tail, out Rune rune, out int consumed);

            if (status != OperationStatus.Done || consumed <= 0)
            {
                // fallback: кодируем текущий char как есть
                rune = new Rune(value[i]);
                consumed = 1;
            }

            int len = rune.EncodeToUtf8(utf8);
            AppendPercentEncodedBytes(sb, utf8.Slice(0, len));

            i += consumed - 1; // consumed = 1 или 2
        }
    }

    static bool IsUnreserved(char c)
        => (c >= 'a' && c <= 'z')
        || (c >= '0' && c <= '9')
        || c == '-' || c == '_' || c == '.';

    static bool IsAllowedChar(char c)
        => (c >= 'a' && c <= 'z')
        || (c >= '0' && c <= '9')
        || c == '@' || c == '.' || c == '-' || c == '_' || c == '=';

    static void AppendPercentEncodedBytes(StringBuilder sb, ReadOnlySpan<byte> bytes)
    {
        const string encodedHex = "0123456789ABCDEF";

        foreach (byte b in bytes)
        {
            sb.Append('%');
            sb.Append(encodedHex[b >> 4]);
            sb.Append(encodedHex[b & 0x0F]);
        }
    }
}
