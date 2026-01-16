using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Net.Http.Headers;
using Shared;

namespace Lampac.Engine.Middlewares
{
    public class Staticache
    {
        private const int DefaultCacheMinutes = 5;
        private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new FileExtensionContentTypeProvider();
        private readonly RequestDelegate _next;
        private readonly IWebHostEnvironment _environment;
        private readonly IMemoryCache _memoryCache;

        public Staticache(RequestDelegate next, IWebHostEnvironment environment, IMemoryCache memoryCache)
        {
            _next = next;
            _environment = environment;
            _memoryCache = memoryCache;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (AppInit.conf?.Staticache?.enable != true)
            {
                await _next(context);
                return;
            }

            if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
            {
                await _next(context);
                return;
            }

            if (context.Request.Headers.ContainsKey(HeaderNames.Range))
            {
                await _next(context);
                return;
            }

            var requestPath = context.Request.Path.Value;
            if (string.IsNullOrWhiteSpace(requestPath) || requestPath.EndsWith("/"))
            {
                await _next(context);
                return;
            }

            var relativePath = requestPath.TrimStart('/');
            if (relativePath.Contains(".."))
            {
                await _next(context);
                return;
            }

            var webRootPath = string.IsNullOrWhiteSpace(_environment?.WebRootPath)
                ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
                : _environment.WebRootPath;
            var filePath = Path.Combine(webRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(filePath))
            {
                await _next(context);
                return;
            }

            try
            {
                var fileInfo = new FileInfo(filePath);
                var cacheKey = $"staticache:{relativePath}:{fileInfo.LastWriteTimeUtc.Ticks}";
                var entry = _memoryCache.GetOrCreate(cacheKey, cacheEntry =>
                {
                    cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(DefaultCacheMinutes);

                    if (!ContentTypeProvider.TryGetContentType(relativePath, out var contentType))
                        contentType = "application/octet-stream";

                    return new StaticacheEntry(File.ReadAllBytes(filePath), contentType, fileInfo.Length, fileInfo.LastWriteTimeUtc);
                });

                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = entry.ContentType;
                context.Response.ContentLength = entry.Length;
                context.Response.Headers[HeaderNames.LastModified] = entry.LastModified.ToString("R");

                if (HttpMethods.IsHead(context.Request.Method))
                    return;

                await context.Response.Body.WriteAsync(entry.Content);
            }
            catch
            {
                await _next(context);
            }
        }

        private sealed record StaticacheEntry(byte[] Content, string ContentType, long Length, DateTimeOffset LastModified);
    }
}
