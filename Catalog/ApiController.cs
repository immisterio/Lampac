using Microsoft.AspNetCore.Mvc;

namespace Catalog.Controllers
{
    public class ApiController : BaseController
    {
        #region catalog.js
        [HttpGet]
        [Route("catalog.js")]
        [Route("catalog/js/{token}")]
        public ActionResult CatalogJS(string token)
        {
            var sb = new StringBuilder(FileCache.ReadAllText("plugins/catalog.js"));

            sb.Replace("{localhost}", host)
              .Replace("{token}", HttpUtility.UrlEncode(token));

            return Content(sb.ToString(), "application/javascript; charset=utf-8");
        }
        #endregion

        [HttpGet]
        [Route("catalog")]
        public ActionResult Index()
        {
            var result = new JObject();

            string dir = Path.Combine(AppContext.BaseDirectory, "catalog", "sites");
            if (!Directory.Exists(dir))
                return ContentTo(result.ToString(Formatting.Indented));

            var sites = new List<(string key, JObject obj, int index)>();

            foreach (var file in Directory.GetFiles(dir, "*.yaml"))
            {
                try
                {
                    var site = Path.GetFileNameWithoutExtension(file);
                    if (string.IsNullOrEmpty(site))
                        continue;

                    var init = ModInit.goInit(site);
                    if (init == null || !init.enable)
                        continue;

                    var siteObj = new JObject();

                    if (init.menu != null)
                    {
                        foreach (var menuItem in init.menu)
                        {
                            if (menuItem?.categories == null)
                                continue;

                            foreach (var cat in menuItem.categories)
                            {
                                string catName = cat.Key;
                                string catCode = cat.Value;
                                if (string.IsNullOrEmpty(catName) || string.IsNullOrEmpty(catCode))
                                    continue;

                                if (!(siteObj[catName] is JObject catObj))
                                {
                                    catObj = new JObject();
                                    siteObj["search"] = $"{host}/catalog/list?plugin={HttpUtility.UrlEncode(site)}";
                                    siteObj[catName] = catObj;
                                }

                                string baseUrl = $"{host}/catalog/list?plugin={HttpUtility.UrlEncode(site)}&cat={HttpUtility.UrlEncode(catCode)}";

                                bool addBaseEntry = true;
                                if (menuItem.format != null)
                                {
                                    if (!menuItem.format.ContainsKey("-"))
                                        addBaseEntry = false;
                                }

                                if (addBaseEntry)
                                {
                                    if (catObj[catName] == null)
                                        catObj[catName] = baseUrl;
                                }

                                if (menuItem.sort != null)
                                {
                                    foreach (var s in menuItem.sort)
                                    {
                                        string sortName = s.Key;
                                        string sortCode = s.Value;
                                        if (string.IsNullOrEmpty(sortName) || string.IsNullOrEmpty(sortCode))
                                            continue;

                                        string sortUrl = baseUrl + "&sort=" + HttpUtility.UrlEncode(sortCode);
                                        if (catObj[sortName] == null)
                                            catObj[sortName] = sortUrl;
                                    }
                                }
                            }
                        }
                    }

                    string siteKey = !string.IsNullOrEmpty(init.plugin) ? init.plugin : init.displayname ?? site;

                    int idx = init.displayindex;
                    if (idx == 0)
                        idx = int.MaxValue - sites.Count;

                    sites.Add((siteKey, siteObj, idx));
                }
                catch { }
            }

            foreach (var s in sites.OrderBy(x => x.index))
                result[s.key] = s.obj;

            return ContentTo(result.ToString(Formatting.Indented));
        }
    }
}
