using Microsoft.AspNetCore.Http;
using Shared.Models.Base;
using Shared.Models.Events;
using Shared.Models.Module;
using Shared.Models.Module.Interfaces;
using Shared.Models.SISI.Base;
using Shared.Services;
using System;
using System.Collections.Generic;

namespace Porntrex;

public class ModInit : IModuleLoaded, IModuleSisi
{
    public static SisiSettings conf;

    public List<SisiModuleItem> Invoke(HttpContext httpContext, RequestModel requestInfo, string host, SisiEventsModel args)
    {
        return new List<SisiModuleItem>()
        {
            new("porntrex.com", conf, "ptx")
        };
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
        conf = ModuleInvoke.Init("Porntrex", new SisiSettings("Porntrex", "https://www.porntrex.com")
        {
            displayindex = 18,
            streamproxy = true,
            rch_access = "apk",
            stream_access = "apk",
            headers_stream = HeadersModel.Init(
                ("referer", "https://www.porntrex.com/")
            ).ToDictionary(),
            headers_image = HeadersModel.Init(
                ("referer", "https://www.porntrex.com/")
            ).ToDictionary()
        });
    }

    string proxyImgMd5key(EventProxyImgMd5key e)
    {
        switch (e.decryptLink.plugin ?? "")
        {
            case "Porntrex":
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
