using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.PlaywrightCore;

namespace Catalog.Controllers
{
    public class CardController : BaseController
    {
        [HttpGet]
        [Route("catalog/card")]
        public async Task<ActionResult> Index(string plugin, string uri, string type)
        {
            var init = ModInit.goInit(plugin)?.Clone();
            if (init == null || !init.enable)
                return BadRequest("init not found");

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string memKey = $"catalog:card:{plugin}:{uri}";

            return await InvkSemaphore(init, memKey, async () =>
            {
                #region html
                if (!hybridCache.TryGetValue(memKey, out string html, inmemory: false))
                {
                    string url = $"{init.host}/{uri}";

                    reset:
                    html =
                        rch.enable ? await rch.Get(url, httpHeaders(init))
                        : init.priorityBrowser == "playwright" ? await PlaywrightBrowser.Get(init, url, httpHeaders(init), proxy.data, cookies: init.cookies)
                        : await Http.Get(url, headers: httpHeaders(init), proxy: proxy.proxy, timeoutSeconds: init.timeout);

                    if (html == null)
                    {
                        if (ModInit.IsRhubFallback(init))
                            goto reset;

                        if (!rch.enable)
                            proxyManager.Refresh();

                        return BadRequest("html");
                    }

                    if (!rch.enable)
                        proxyManager.Success();

                    hybridCache.Set(memKey, html, cacheTime(init.cache_time, init: init), inmemory: false);
                }
                #endregion

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var node = doc.DocumentNode;
                var parse = init.cardParse;

                string name = ModInit.nodeValue(node, parse.name, host);
                string original_name = ModInit.nodeValue(node, parse.original_name, host);
                string year = ModInit.nodeValue(node, parse.year, host);

                var jo = new JObject()
                {
                    ["id"] = CrypTo.md5($"{plugin}:{uri}"),
                    ["image"] = ModInit.nodeValue(node, parse.image, host),
                    ["overview"] = ModInit.nodeValue(node, parse.description, host)
                };

                if (type == "tv")
                {
                    jo["first_air_date"] = year;
                    jo["name"] = name;

                    if (!string.IsNullOrEmpty(original_name))
                        jo["original_name"] = original_name;
                }
                else
                {
                    jo["release_date"] = year;
                    jo["title"] = name;

                    if (!string.IsNullOrEmpty(original_name))
                        jo["original_title"] = original_name;
                }

                if (parse.args != null)
                {
                    foreach (var arg in parse.args)
                    {
                        string val = ModInit.nodeValue(node, arg, host);
                        if (!string.IsNullOrEmpty(val))
                        {
                            if (!string.IsNullOrEmpty(val))
                                jo.Add(arg.name_arg, val);
                        }
                    }
                }

                return ContentTo(JsonConvert.SerializeObject(jo));
            });
        }
    }
}
