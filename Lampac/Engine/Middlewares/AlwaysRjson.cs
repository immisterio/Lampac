using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;
using Shared;
using System;
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

            var builder = new QueryBuilder();

            foreach (var kv in QueryHelpers.ParseQuery(context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty))
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
    }
}
