using Shared;
using Shared.Models.AppConf;
using Shared.Models.Events;
using Shared.Models.Module;
using Shared.Models.Module.Interfaces;
using Shared.Services;
using System.Collections.Generic;

namespace TmdbProxy;

public class ModInit : IModuleLoaded
{
    public static string modpath;
    public static ModuleConf conf;

    public void Loaded(InitspaceModel baseconf)
    {
        modpath = baseconf.path;

        updateConf();
        EventListener.UpdateInitFile += updateConf;

        foreach (var m in conf.limit_map)
            CoreInit.conf.WAF.limit_map.Insert(0, m);
    }

    public void Dispose()
    {
        EventListener.UpdateInitFile -= updateConf;
    }

    void updateConf()
    {
        conf = ModuleInvoke.Init("tmdb", new ModuleConf()
        {
            responseContentLength = true,
            httpversion = 2,
            cache_api = 60 * 24, // 24h
            cache_img = 60 * 72, // 3d
            limit_map = new List<WafLimitRootMap>()
            {
                new("^/tmdb/", new WafLimitMap { limit = 50, second = 1 })
            }
        });
    }
}
