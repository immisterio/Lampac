using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Shared.Attributes;
using Shared.Models.AppConf;
using Shared.Services;
using Shared.Services.Pools;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Core.Middlewares;

public class StaticacheWriter
{
    #region static
    private readonly RequestDelegate _next;

    public StaticacheWriter(RequestDelegate next)
    {
        _next = next;
    }
    #endregion

    async public Task InvokeAsync(HttpContext httpContext)
    {
        var stc = httpContext.Features.Get<StaticacheFeature>();
        if (stc == null)
        {
            await _next(httpContext);
            return;
        }

        using (var buff = new BufferWriterPool<byte>(BufferWriterPoolType.Small))
        {
            httpContext.Features.Set(buff);
            httpContext.Response.Headers["X-StatiCache-Status"] = "MISS";

            await _next(httpContext);

            if (buff.WrittenCount > 0)
            {
                #region Дожимаем httpContext
                var ex = httpContext.Features.Get<StatiCacheEntry>()?.ex
                    ?? DateTimeOffset.Now.AddMinutes(stc.cacheMinutes);

                if (httpContext.Response.StatusCode != 200)
                    ex = DateTimeOffset.Now.AddMinutes(1);

                int contentLength = (int)(httpContext.Response?.ContentLength ?? 0);
                if (contentLength > 0 && contentLength != buff.WrittenCount)
                    ex = DateTimeOffset.Now.AddMinutes(1);

                string ext = "bin";
                bool isMedia = false;
                string contentType = httpContext.Response.ContentType;

                if (contentType != null)
                {
                    if (contentType.StartsWith("text/html"))
                        ext = "html";
                    else if (contentType.StartsWith("application/json"))
                        ext = "json";
                    else if (contentType.StartsWith("application/javascript"))
                        ext = "js";
                    else if (contentType.StartsWith("text/css"))
                        ext = "css";
                    else if (contentType.StartsWith("image/svg+xml"))
                        ext = "svg";
                    else if (contentType.StartsWith("image/png"))
                    {
                        ext = "png";
                        isMedia = true;
                    }
                    else if (contentType.StartsWith("image/jpeg"))
                    {
                        ext = "jpg";
                        isMedia = true;
                    }
                    else if (contentType.StartsWith("image/webp"))
                    {
                        ext = "webp";
                        isMedia = true;
                    }
                }

                if (isMedia && contentLength == 0)
                {
                    contentLength = buff.WrittenCount;
                    httpContext.Response.ContentLength = contentLength;
                }

                if (contentLength > 0)
                    httpContext.Response.Headers[HeaderNames.CacheControl] = "public,max-age=86400,immutable";
                #endregion

                #region Сбрасываем поток клиенту
                const int chunkSize = 32 * 1024;

                var source = buff.WrittenSpan;
                var bodyWriter = httpContext.Response.BodyWriter;

                do
                {
                    int bytesToWrite = Math.Min(source.Length, chunkSize);

                    ReadOnlySpan<byte> chunk = source.Slice(0, bytesToWrite);
                    Span<byte> destination = bodyWriter.GetSpan(chunkSize);

                    chunk.CopyTo(destination);
                    bodyWriter.Advance(bytesToWrite);

                    source = source.Slice(bytesToWrite);
                }
                while (!source.IsEmpty);

                await httpContext.Response.CompleteAsync();
                #endregion

                #region Сохраняем на диск
                string cachekey = stc.cachekey;
                var sm = new SemaphorManager(cachekey, TimeSpan.FromSeconds(5));

                try
                {
                    if (DateTimeOffset.Now > ex)
                        return;

                    bool _acquired = await sm.WaitAsync();
                    if (!_acquired)
                        return;

                    long exTicks = ex.ToUnixTimeMilliseconds();
                    string cachefile = Staticache.GetFilePath(cachekey, exTicks, contentLength, ext);

                    await using (var fileStream = new FileStream(cachefile, FileMode.Create, FileAccess.Write, FileShare.None,
                        bufferSize: PoolInvk.bufferSize,
                        options: FileOptions.Asynchronous))
                    {
                        await fileStream.WriteAsync(buff.WrittenMemory);
                    }

                    Staticache.cacheFiles[cachekey] = new StaticacheCacheModel(exTicks, ext, (short)httpContext.Response.StatusCode, contentLength);
                }
                catch (Exception exception)
                {
                    Serilog.Log.Error(exception, "CatchId={CatchId}", "id_23a31ad1");
                }
                finally
                {
                    sm.Release();
                }
                #endregion
            }
        }
    }
}
