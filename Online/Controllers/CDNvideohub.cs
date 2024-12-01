using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;

namespace Lampac.Controllers.LITE
{
    public class CDNvideohub : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/cdnvideohub")]
        async public Task<ActionResult> Index(string title, string original_title, long kinopoisk_id, bool origsource = false, bool rjson = false)
        {
            var init = AppInit.conf.CDNvideohub.Clone();
            if (!init.enable || init.rip)
                return OnError();

            if (init.rhub && !AppInit.conf.rch.enable)
                return ShowError(RchClient.ErrorMsg);

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            var proxyManager = new ProxyManager("cdnvideohub", init);
            var proxy = proxyManager.Get();

            string memKey = rch.ipkey($"cdnvideohub:view:{kinopoisk_id}", proxyManager);
            if (!hybridCache.TryGetValue(memKey, out string file))
            {
                if (rch.IsNotConnected())
                    return ContentTo(rch.connectionMsg);

                string uri = $"{init.corsHost()}/playerjs?partner=20&kid={kinopoisk_id}&src=sv";
                string embed = rch.enable ? await rch.Get(uri) : await HttpClient.Get(uri, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));
                if (embed == null)
                    return OnError(proxyManager, refresh_proxy: !rch.enable);

                file = Regex.Match(embed, "'file': '([^']+)'").Groups[1].Value;
                if (string.IsNullOrEmpty(file))
                    return OnError();

                if (!rch.enable)
                    proxyManager.Success();

                file = file.Replace("u0026", "&").Replace("\\", "");
                hybridCache.Set(memKey, file, cacheTime(rch.enable ? 5 : 20, init: init));
            }

            if (origsource)
                return Json(new { file });

            var mtpl = new MovieTpl(title, original_title);
            mtpl.Append("По умолчанию", HostStreamProxy(init, file, proxy: proxy));

            return ContentTo(rjson ? mtpl.ToJson() : mtpl.ToHtml());
        }
    }
}
