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
            var init = AppInit.conf.CDNvideohub;
            if (!init.enable)
                return OnError();

            var proxyManager = new ProxyManager("cdnvideohub", init);
            var proxy = proxyManager.Get();

            string memKey = $"cdnvideohub:view:{kinopoisk_id}";
            if (!hybridCache.TryGetValue(memKey, out string file))
            {
                string embed = await HttpClient.Get($"{init.corsHost()}/playerjs?partner=20&kid={kinopoisk_id}&src=sv", timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));
                if (embed == null)
                    return OnError(proxyManager);

                file = Regex.Match(embed, "'file': '([^']+)'").Groups[1].Value;
                if (string.IsNullOrEmpty(file))
                    return OnError();

                proxyManager.Success();
                hybridCache.Set(memKey, file.Replace("u0026", "&").Replace("\\", ""), cacheTime(20, init: init));
            }

            if (origsource)
                return Json(new { file });

            var mtpl = new MovieTpl(title, original_title);
            mtpl.Append("По умолчанию", HostStreamProxy(init, file, proxy: proxy));

            return ContentTo(rjson ? mtpl.ToJson() : mtpl.ToHtml());
        }
    }
}
