using GStreamer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Attributes;
using Shared.Services;
using Shared.Services.Pools;
using System.Threading.Tasks;
using System.Web;

namespace GStreamer;

public class GStreamerController : BaseController
{
    #region gst.js
    [AllowAnonymous]
    [Staticache(10, always: true, setHeadersNoCache: true)]
    [HttpGet("/gst.js")]
    [HttpGet("/gst/js/{token}")]
    public ActionResult GstJs(string token)
    {
        if (!ModInit.conf.enable)
            return Content(string.Empty, "application/javascript; charset=utf-8");

        var plugin = FileCache.ReadAllText($"{ModInit.modpath}/plugin.js", "gst.js")
            .Replace("{localhost}", CoreInit.Host(HttpContext))
            .Replace("{token}", HttpUtility.UrlEncode(token));

        return ContentTo(plugin, "application/javascript; charset=utf-8");
    }
    #endregion

    #region start.m3u8
    [HttpGet("/gst/start.m3u8")]
    public async Task<ActionResult> Start(string link, string uid)
    {
        if (!ModInit.conf.enable)
            return StatusCode(403);

        var gstask = await GService.GetOrAdd(link, uid);
        if (gstask == null)
            return StatusCode(502);

        return Redirect($"/gst/{gstask.id}/master.m3u8");
    }
    #endregion

    #region master.m3u8
    [AllowAnonymous]
    [HttpGet("/gst/{id}/master.m3u8")]
    public ActionResult MasterPlaylist(ulong id)
    {
        SetHeadersNoCache();

        var gstask = GService.Get(id);
        if (gstask == null)
            return NotFound();

        int duration = gstask.probe.DurationSeconds;
        if (0 >= duration)
            duration = 200 * 60; // 200 min

        int count = duration / GStask.segmentSeconds;

        var playlist = StringBuilderPool.Rent();

        try
        {
            playlist.AppendLine("#EXTM3U");
            playlist.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");
            playlist.AppendLine("#EXT-X-VERSION:7");
            playlist.AppendLine("#EXT-X-TARGETDURATION:6");
            playlist.AppendLine("#EXT-X-MEDIA-SEQUENCE:0");
            playlist.AppendLine("#EXT-X-MAP:URI=\"init.mp4\"");

            for (int i = 0; i < count; i++)
            {
                playlist.AppendLine("#EXTINF:6.00,");

                playlist
                    .Append("seg/")
                    .Append(i)
                    .AppendLine(".m4s");
            }

            playlist.AppendLine("#EXT-X-ENDLIST");

            return ContentTo(
                playlist,
                "application/vnd.apple.mpegurl; charset=utf-8"
            );
        }
        finally
        {
            StringBuilderPool.Return(playlist);
        }
    }
    #endregion

    #region init.mp4
    [AllowAnonymous]
    [HttpGet("/gst/{id}/init.mp4")]
    public async Task VideoInit(ulong id)
    {
        SetHeadersNoCache();

        if (Request.Headers.ContainsKey("Range"))
        {
            Response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
            Response.Headers.AcceptRanges = "none";
            return;
        }

        var gstask = GService.Get(id);
        if (gstask == null)
        {
            HttpContext.Response.StatusCode = 404;
            return;
        }

        if (gstask.initMp4 == null)
        {
            await gstask.semaphore.WaitAsync(HttpContext.RequestAborted);

            try
            {
                if (gstask.initMp4 == null)
                    gstask.GetSegment(-1, HttpContext.RequestAborted);
            }
            finally
            {
                gstask.semaphore.Release();
            }
        }

        if (gstask.initMp4 == null)
        {
            HttpContext.Response.StatusCode = 502;
            return;
        }

        Response.ContentType = "video/mp4";
        Response.Headers.AcceptRanges = "none";
        Response.Headers.ContentLength = gstask.initMp4.Length;

        await Response.Body.WriteAsync(gstask.initMp4, 0, gstask.initMp4.Length, HttpContext.RequestAborted);
    }
    #endregion

    #region seg
    [AllowAnonymous]
    [HttpGet("/gst/{id}/seg/{index:int}.m4s")]
    public async Task VideoSeg(ulong id, int index)
    {
        SetHeadersNoCache();

        var gstask = GService.Get(id);
        if (gstask == null)
        {
            HttpContext.Response.StatusCode = 404;
            return;
        }

        await gstask.semaphore.WaitAsync(HttpContext.RequestAborted);

        try
        {
            if (gstask.lastSentSegment == -1)
            {
                gstask.lastSentSegment = index;
            }
            else if (gstask.lastSentSegment != index)
            {
                if (index != gstask.lastSentSegment + 1)
                {
                    gstask.lastSentSegment = index;
                    bool seekok = gstask.Seek(index * GStask.segmentSeconds);
                    if (!seekok)
                    {
                        HttpContext.Response.StatusCode = 502;
                        return;
                    }
                }

                gstask.lastSentSegment = index;
            }

            Segment seg = gstask.GetSegment(index, HttpContext.RequestAborted);
            if (seg.audio == null || seg.video == null)
            {
                HttpContext.Response.StatusCode = 502;
                return;
            }

            HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            Response.ContentType = "video/mp4";
            Response.Headers.AcceptRanges = "none";
            Response.Headers.ContentLength = seg.video.Length + seg.audio.Length;

            await seg.video.CopyToAsync(Response.Body, HttpContext.RequestAborted);
            await seg.audio.CopyToAsync(Response.Body, HttpContext.RequestAborted);
        }
        finally
        {
            gstask.semaphore.Release();
        }
    }
    #endregion
}
