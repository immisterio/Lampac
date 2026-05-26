using Microsoft.AspNetCore.Http;
using Shared.Models.Base;
using Shared.Models.Events;
using Shared.Models.Module;
using Shared.Models.Module.Interfaces;
using Shared.Services;
using System;
using System.Collections.Generic;

namespace PornHub;

public class ModInit : IModuleLoaded, IModuleSisi
{
    public static ModuleConf conf;

    public List<SisiModuleItem> Invoke(HttpContext httpContext, RequestModel requestInfo, string host, SisiEventsModel args)
    {
        var channels = new List<SisiModuleItem>()
        {
            new("pornhub.com", conf.PornHub, "phub"),
            new("pornhubpremium.com", conf.PornHubPremium, "phubprem")
        };

        if (args.lgbt)
        {
            channels.Add(new("phubgay", conf.PornHub, "phubgay", 10_100));
            channels.Add(new("phubtrans", conf.PornHub, "phubsml", 10_101));
        }

        return channels;
    }

    public void Loaded(InitspaceModel baseconf)
    {
        updateConf();
        EventListener.UpdateInitFile += updateConf;
        EventListener.ProxyImgMd5key += proxyImgMd5key;
    }

    public void Dispose()
    {
        EventListener.UpdateInitFile -= updateConf;
        EventListener.ProxyImgMd5key -= proxyImgMd5key;
    }

    void updateConf()
    {
        conf = ModuleInvoke.DeserializeInit(new ModuleConf());
    }

    string proxyImgMd5key(EventProxyImgMd5key e)
    {
        switch (e.decryptLink.plugin ?? "")
        {
            case "PornHub":
                {
                    ReadOnlySpan<char> original = e.href
                        .AsSpan()
                        .Slice(8); // https?://

                    int q = original.IndexOf('?');
                    if (q >= 0)
                        original = original.Slice(0, q);

                    int slash = original.IndexOf('/');
                    if (q >= 0)
                        original = original.Slice(slash);

                    return string.Concat(e.decryptLink.plugin, ":", original);
                }
            default:
                return default;
        }
    }
}
