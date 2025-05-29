using Lampac.Engine.CORE;
using Microsoft.AspNetCore.Mvc;
using Online;
using Shared.Engine.CORE;
using Shared.Model.Online.Kinotochka;
using Shared.Model.Templates;
using System.Threading.Tasks;

namespace Lampac.Controllers.LITE
{
    public class RutubeMovie : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/rutubemovie")]
        async public Task<ActionResult> Index(string title, int year, int serial, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.RutubeMovie);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            string searchTitle = StringConvert.SearchName(title);
            if (string.IsNullOrEmpty(searchTitle) || year == 0)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();
            reset: var rch = new RchClient(HttpContext, host, init, requestInfo);

            if (serial == 1)
            {
                return OnError();
            }
            else
            {
                var cache = await InvokeCache<EmbedModel>("rutubemovie:FILMS", cacheTime(60 * 10, init: init), rch.enable ? null : proxyManager, async res =>
                {
                    if (rch.IsNotConnected())
                        return res.Fail(rch.connectionMsg);

                    string file = rch.enable ? await rch.Get(init.host, httpHeaders(init)) : await HttpClient.Get(init.host, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));
                    if (string.IsNullOrEmpty(file))
                        return res.Fail("content");

                    return new EmbedModel() { content = file.Replace("\r", "") };
                });

                if (IsRhubFallback(cache, init))
                    goto reset;

                return OnResult(cache, () =>
                {
                    var mtpl = new MovieTpl(title);

                    foreach (string EXTINF in cache.Value.content.Split("#EXTINF:"))
                    {
                        // Оптимизированная строка парсинга имени
                        string[] extinfParts = EXTINF.Split('\n')[0].Split("\",");
                        string name = extinfParts.Length > 1 ? StringConvert.SearchName(extinfParts[1]) : string.Empty;
                        if (!string.IsNullOrEmpty(name))
                        {
                            if (name.StartsWith(searchTitle) && (name.Contains(year.ToString()) || name.Contains((year + 1).ToString()) || name.Contains((year - 1).ToString())))
                            {
                                // Следующая строка - ссылка
                                // Безопасно получаем следующую строку после #EXTINF
                                var extinfLines = EXTINF.Split('\n');
                                if (extinfLines.Length > 1)
                                {
                                    var nextLine = extinfLines[1].Trim();
                                    if (nextLine.StartsWith("http") && (nextLine.Contains("vod.plvideo") || nextLine.Contains("rutube.ru")))
                                    {
                                        if (nextLine.Contains("vod.plvideo"))
                                            nextLine += "#.m3u8";

                                        mtpl.Append(nextLine.Contains("rutube.ru") ? "rutube" : "plvideo", HostStreamProxy(init, nextLine, proxy: proxy), vast: init.vast);
                                    }
                                }
                            }
                        }
                    }

                    return rjson ? mtpl.ToJson() : mtpl.ToHtml();

                }, gbcache: !rch.enable);
            }
        }
    }
}
