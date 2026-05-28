using Microsoft.AspNetCore.Http;
using Shared.Models.Base;
using Shared.Models.Events;
using Shared.Models.Module;
using Shared.Models.Module.Interfaces;
using Shared.Models.Online.Settings;
using Shared.Services;
using Shared.Services.Utilities;
using System.Collections.Generic;
using Shared;
using System.Linq;

namespace VeoVeo;

public class ModInit : IModuleLoaded, IModuleOnline, IModuleOnlineSpider
{
    public static OnlinesSettings conf;

    #region database
    static List<Movie> databaseCache;
    public static Dictionary<string, Movie> databaseById;

    public static IEnumerable<Movie> database
        => databaseCache ?? JsonHelper.IEnumerableReader<Movie>("data/veoveo.json");
    #endregion

    public List<ModuleOnlineItem> Invoke(HttpContext httpContext, RequestModel requestInfo, string host, OnlineEventsModel args)
    {
        return new List<ModuleOnlineItem>()
        {
            new(conf)
        };
    }

    public List<ModuleOnlineSpiderItem> Spider(HttpContext httpContext, RequestModel requestInfo, string host, OnlineSpiderModel args)
    {
        return new List<ModuleOnlineSpiderItem>()
        {
            new(conf, "veoveo-spider")
        };
    }

    public void Loaded(InitspaceModel baseconf)
    {
        CoreInit.conf.online.with_search.Add("veoveo");

        updateConf();
        EventListener.UpdateInitFile += updateConf;
        EventListener.OnlineApiQuality += onlineApiQuality;

        if (CoreInit.conf.lowMemoryMode == false)
        {
            databaseCache = JsonHelper.ListReader<Movie>("data/veoveo.json", 130_000)
                .GetAwaiter()
                .GetResult();

            databaseById = new Dictionary<string, Movie>();

            foreach (var movie in databaseCache.OrderByDescending(i => i.id))
            {
                if (movie.kinopoiskId > 0)
                    databaseById.TryAdd(movie.kinopoiskId.ToString(), movie);

                if (movie.imdbId != null)
                    databaseById.TryAdd(movie.imdbId, movie);

                string stitle = SearchNameTo.Convert(movie.title);
                if (stitle != null)
                    databaseById.TryAdd(stitle, movie);

                string sorig = SearchNameTo.Convert(movie.originalTitle);
                if (sorig != null)
                    databaseById.TryAdd(sorig, movie);
            }
        }
    }

    public void Dispose()
    {
        EventListener.UpdateInitFile -= updateConf;
        EventListener.OnlineApiQuality -= onlineApiQuality;
        databaseCache?.Clear();
        databaseCache = null;
    }

    void updateConf()
    {
        conf = ModuleInvoke.Init("VeoVeo", new OnlinesSettings("VeoVeo", "https://api.rstprgapipt.com")
        {
            displayindex = 550,
            httpversion = 2,
            stream_access = "apk,cors,web"
        });
    }

    string onlineApiQuality(EventOnlineApiQuality e)
    {
        return e.balanser switch
        {
            "veoveo" => " ~ 1080p",
            _ => null
        };
    }
}
