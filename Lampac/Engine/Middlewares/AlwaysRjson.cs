using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class AlwaysRjson
    {
        private readonly RequestDelegate _next;

        public AlwaysRjson(RequestDelegate next)
        {
            _next = next;
        }

        public Task Invoke(HttpContext context)
        {
            if (!AppInit.conf.always_rjson)
                return _next(context);

            var query = QueryHelpers.ParseQuery(context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty);

            if (!RequiresRewrite(query))
                return _next(context);

            var builder = new QueryBuilder();

            foreach (var kv in query)
            {
                if (string.Equals(kv.Key, "rjson", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var value in kv.Value)
                    builder.Add(kv.Key, value);
            }

            builder.Add("rjson", "true");

            context.Request.QueryString = builder.ToQueryString();

            return _next(context);
        }

        static bool RequiresRewrite(IDictionary<string, StringValues> query)
        {
            if (!query.TryGetValue("rjson", out var value))
                return true;

            for (int i = 0; i < value.Count; i++)
            {
                if (string.Equals(value[i], "true", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }
    }
}
