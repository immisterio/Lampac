using Microsoft.AspNetCore.Http;
using Shared.Models.Base;
using Shared.Models.Events;
using Shared.Models.Module;
using Shared.Models.Module.Interfaces;
using Shared.Services;
using System.Collections.Generic;

namespace SakhTV;

public class ModInit : IModuleLoaded, IModuleOnline
{
    public static ModuleConf conf;

    public List<ModuleOnlineItem> Invoke(HttpContext httpContext, RequestModel requestInfo, string host, OnlineEventsModel args)
    {
        return new List<ModuleOnlineItem>()
        {
            new(conf)
        };
    }

    public void Loaded(InitspaceModel baseconf)
    {
        updateConf();
        EventListener.UpdateInitFile += updateConf;
        EventListener.OnlineApiQuality += onlineApiQuality;
    }

    public void Dispose()
    {
        EventListener.UpdateInitFile -= updateConf;
        EventListener.OnlineApiQuality -= onlineApiQuality;
    }

    void updateConf()
    {
        /// https://ru-api.sakh.tv
        conf = ModuleInvoke.Init("SakhTV", new ModuleConf("SakhTV", "https://api.sakh.tv")
        {
            enable = false,
            displayindex = 340,
            httpversion = 2,
            rhub_safety = false,
            stream_access = "apk,cors,web",
            APP_VERSION = "1.2.0-tv",
            app_id = "5",
            release = "12",
            userAgent = "Xiaomi Mi BOX 4"
        });
    }

    string onlineApiQuality(EventOnlineApiQuality e)
    {
        return e.balanser switch
        {
            "sakhtv" => " ~ 1080p",
            _ => null
        };
    }
}
