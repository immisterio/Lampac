using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Shared.Models.Events;
using Shared.Models.Module;
using Shared.Models.Module.Interfaces;
using Shared.Services.Pools;
using System;
using System.Threading.Tasks;

namespace ForkXML;

public class ModInit : IModuleLoaded
{
    public void Loaded(InitspaceModel baseconf)
    {
        EventListener.BadInitialization += BadInitialization;
        EventListener.SisiChannels += SisiAPI.Channels;
        EventListener.SisiPlaylistResult += SisiAPI.PlaylistResult;
        EventListener.SisiOnResult += SisiAPI.OnResult;
        EventListener.OnlineChannels += OnlineAPI.Channels;
        EventListener.OnlineContentTpl += OnlineAPI.ContentTpl;
        EventListener.VideoTpl += OnlineAPI.VideoTpl;
    }

    public void Dispose()
    {
        EventListener.BadInitialization -= BadInitialization;
        EventListener.SisiChannels -= SisiAPI.Channels;
        EventListener.SisiPlaylistResult -= SisiAPI.PlaylistResult;
        EventListener.SisiOnResult -= SisiAPI.OnResult;
        EventListener.OnlineChannels -= OnlineAPI.Channels;
        EventListener.OnlineContentTpl -= OnlineAPI.ContentTpl;
        EventListener.VideoTpl -= OnlineAPI.VideoTpl;
    }

    Task<ActionResult> BadInitialization(EventBadInitialization e)
    {
        if (IsForkPlayer(e.httpContext))
        {
            e.init.rhub = false;
            e.init.streamproxy = true;
        }

        return Task.FromResult<ActionResult>(default);
    }

    public static bool IsForkPlayer(HttpContext httpContext)
    {
        if (httpContext.Request.Query.TryGetValue("initial", out StringValues initial) && initial.Count > 0)
            return initial[0].StartsWith("ForkXML", StringComparison.OrdinalIgnoreCase);

        return false;
    }

    public static string clearArgs(IQueryCollection query)
    {
        bool first = true;
        var args = StringBuilderPool.ThreadInstance;

        foreach (var q in query)
        {
            if (q.Key is "box_client" or "box_mac" or "pg" or "initial" or "platform" or "country" or "tvp" or "hw")
                continue;

            if (!string.IsNullOrEmpty(q.Key) && !string.IsNullOrEmpty(q.Value))
            {
                if (!first)
                    args.Append("&");

                args.Append(q.Key).Append("=").Append(q.Value);
                first = false;
            }
        }

        return args.ToString();
    }
}
