using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Primitives;
using Shared;
using Shared.Services.Pools;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Core.Middlewares;

public class BaseMod
{
    private readonly RequestDelegate _next;

    public BaseMod(RequestDelegate next)
    {
        _next = next;
    }

    public Task Invoke(HttpContext context)
    {
        if (CoreInit.conf.openstat.enable)
            RequestInfoStats.Increment(RequestStatsType.Base);

        if (CoreInit.conf.BaseModule.BlockedBots && IsBlockedUserAgent(context.Request.Headers.UserAgent))
        {
            if (CoreInit.conf.openstat.enable)
                RequestInfoStats.Increment(RequestStatsType.Bot);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        if (HttpMethods.IsOptions(context.Request.Method))
            return _next(context);

        if (!HttpMethods.IsGet(context.Request.Method) &&
            !HttpMethods.IsPost(context.Request.Method))
            return Task.CompletedTask;

        if (!CoreInit.conf.BaseModule.ValidateRequest)
            return _next(context);

        if (!IsValidPath(context.Request.Path.Value))
        {
            context.Response.StatusCode = 400;
            return Task.CompletedTask;
        }

        var builder = new QueryBuilder();
        var dict = new Dictionary<string, StringValues>(context.Request.Query.Count, StringComparer.OrdinalIgnoreCase);

        bool changeQuery = false;
        var sbQuery = StringBuilderPool.ThreadInstance;

        foreach (var q in context.Request.Query)
        {
            if (IsValidQueryName(q.Key))
            {
                string val = ValidQueryValue(sbQuery, q.Key, q.Value, ref changeQuery);

                if (dict.TryAdd(q.Key, val))
                    builder.Add(q.Key, val);
            }
            else
            {
                context.Response.StatusCode = 400;
                return Task.CompletedTask;
            }
        }

        if (changeQuery)
        {
            context.Request.QueryString = builder.ToQueryString();
            context.Request.Query = new QueryCollection(dict);
        }

        return _next(context);
    }

    #region IsValid
    static bool IsValidPath(ReadOnlySpan<char> path)
    {
        if (path.IsEmpty)
            return false;

        foreach (var whitePath in CoreInit.BaseModPathWhiteList)
        {
            if (path.StartsWith(whitePath, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (char ch in path)
        {
            if (
                ch == '/' || ch == '-' || ch == '.' || ch == '_' ||
                ch == ':' || ch == '+' || ch == '=' ||
                (ch >= 'A' && ch <= 'Z') ||
                (ch >= 'a' && ch <= 'z') ||
                (ch >= '0' && ch <= '9')
            )
            {
                continue;
            }

            return false;
        }

        return true;
    }


    static bool IsValidQueryName(ReadOnlySpan<char> path)
    {
        if (path.IsEmpty)
            return false;

        foreach (char ch in path)
        {
            if (
                ch == '-' || ch == '_' ||
                (ch >= 'A' && ch <= 'Z') ||
                (ch >= 'a' && ch <= 'z') ||
                (ch >= '0' && ch <= '9') ||
                ch == '[' || ch == ']' ||
                ch == '.' // tmdb
            )
            {
                continue;
            }

            return false;
        }

        return true;
    }

    static string ValidQueryValue(StringBuilder sb, string name, StringValues values, ref bool changeQuery)
    {
        if (values.Count == 0)
            return string.Empty;

        string value = values[0];
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (CoreInit.BaseModValidQueryValueWhiteList.Contains(name))
            return value;

        sb.Clear();

        bool isSearcName = name is "search" or "title" or "original_title" or "t";

        foreach (char ch in value)
        {
            if (
                ch == '/' || ch == ':' || ch == '?' || ch == '&' || ch == '=' || ch == '.' || // ссылки
                ch == '-' || ch == '_' || ch == ' ' || ch == ',' || // base
                (ch >= '0' && ch <= '9') ||
                ch == '@' || // email
                ch == '+' || // aes
                ch == '*' || // merchant
                ch == '|' || // tmdb
                char.IsLetter(ch) // ← любые буквы Unicode
            )
            {
                sb.Append(ch);
                continue;
            }

            if (isSearcName)
            {
                if (
                    char.IsDigit(ch) || // ← символ цифрой Unicode
                    ch == '\'' || ch == '!' || ch == ',' || ch == '+' || ch == '~' || ch == '"' || ch == ';' ||
                    ch == '(' || ch == ')' || ch == '[' || ch == ']' || ch == '{' || ch == '}' || ch == '«' || ch == '»' || ch == '“' || ch == '”' ||
                    ch == '$' || ch == '%' || ch == '^' || ch == '#' || ch == '×'
                )
                {
                    sb.Append(ch);
                    continue;
                }
            }

            changeQuery = true;
        }

        return sb.ToString();
    }
    #endregion

    #region BlockedUserAgent
    static readonly string[] BlockedUserAgentPatterns =
    {
        // Search bots
        "googlebot",
        "adsbot-google",
        "mediapartners-google",
        "bingbot",
        "yandex",
        "baiduspider",
        "duckduckbot",
        "slurp",
        "sogou",
        "petalbot",

        // AI bots
        "gptbot",
        "chatgpt-user",
        "claudebot",
        "anthropic-ai",
        "ccbot",
        "perplexitybot",
        "bytespider",
        "amazonbot",
        "cohere-ai",
        "imagesiftbot",
        "ai2bot"
    };

    static bool IsBlockedUserAgent(StringValues userAgent)
    {
        if (userAgent.Count == 0)
            return false;

        string ua = userAgent[0];
        if (string.IsNullOrEmpty(ua))
            return false;

        foreach (var pattern in BlockedUserAgentPatterns)
        {
            if (ua.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
    #endregion
}
