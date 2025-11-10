using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Controllers
{
    public class ApiController : BaseController
    {
        #region catalog.js
        [HttpGet]
        [AllowAnonymous]
        [Route("catalog.js")]
        [Route("catalog/js/{token}")]
        public ActionResult CatalogJS(string token)
        {
            var sb = new StringBuilder(FileCache.ReadAllText("plugins/catalog.js"));

            sb.Replace("{localhost}", host)
              .Replace("{token}", HttpUtility.UrlEncode(token))
              .Replace("catalogs:{}", $"catalogs:{jsonCatalogs()}");

            return Content(sb.ToString(), "application/javascript; charset=utf-8");
        }
        #endregion

        [HttpGet]
        [Route("catalog")]
        public ActionResult Index()
        {
            return ContentTo(jsonCatalogs());
        }


        string jsonCatalogs()
        {
            var result = new JObject();

            string dir = Path.Combine(AppContext.BaseDirectory, "catalog", "sites");
            if (!Directory.Exists(dir))
                return result.ToString(Formatting.None);

            #region sites
            var sites = new List<(string key, JObject obj, int index)>();

            foreach (var file in Directory.GetFiles(dir, "*.yaml"))
            {
                try
                {
                    var site = Path.GetFileNameWithoutExtension(file);
                    if (string.IsNullOrEmpty(site))
                        continue;

                    var init = ModInit.goInit(site);
                    if (init == null || !init.enable || init.menu == null || init.hide)
                        continue;

                    var siteObj = new JObject();

                    foreach (var menuItem in init.menu)
                    {
                        if (menuItem?.categories == null || menuItem.categories.Count == 0)
                            continue;

                        foreach (var cat in menuItem.categories)
                        {
                            string catName = cat.Key;
                            string catCode = cat.Value;

                            if (!(siteObj[catName] is JObject catObj))
                            {
                                catObj = new JObject();

                                if (init.search != null)
                                    siteObj["search"] = $"/catalog/list?plugin={HttpUtility.UrlEncode(menuItem.catalog ?? site)}";

                                siteObj["search_lazy"] = init.search_lazy;

                                if (!string.IsNullOrEmpty(init.catalog_key))
                                    siteObj["catalog_key"] = init.catalog_key;

                                if (!string.IsNullOrEmpty(menuItem.defaultName))
                                    siteObj["defaultName"] = menuItem.defaultName;

                                siteObj[catName] = catObj;
                            }

                            string baseUrl = $"/catalog/list?plugin={HttpUtility.UrlEncode(menuItem.catalog ?? site)}&cat={HttpUtility.UrlEncode(catCode)}";

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

                    string siteKey = !string.IsNullOrEmpty(init.plugin) ? init.plugin : init.displayname ?? site;

                    int idx = init.displayindex;
                    if (idx == 0)
                        idx = int.MaxValue - sites.Count;

                    sites.Add((siteKey, siteObj, idx));
                }
                catch { }
            }
            #endregion

            #region result
            foreach (var s in sites.OrderBy(x => x.index))
            {
                result[s.key] = new JObject();

                if (s.obj.ContainsKey("search"))
                    result[s.key]["search"] = s.obj["search"];

                result[s.key]["search_lazy"] = s.obj["search_lazy"];

                string catalog_key = s.obj.ContainsKey("catalog_key") ? s.obj["catalog_key"]?.ToString() : null;
                string defaultName = s.obj.ContainsKey("defaultName") ? s.obj["defaultName"]?.ToString() : null;

                var menu = new JObject();
                var main = new JObject();

                foreach (var prop in s.obj.Properties())
                {
                    if (!(prop.Value is JObject catObj))
                        continue;

                    foreach (var inner in catObj.Properties())
                    {
                        string pname = prop.Name;
                        if (pname.StartsWith("["))
                            pname = prop.Name.Split(']')[1].Trim();

                        if (pname != inner.Name)
                            main[$"{pname} • {inner.Name.ToLower()}"] = inner.Value;
                        else
                            main[pname] = inner.Value;

                        if (!menu.ContainsKey(pname) || (catalog_key != null && catalog_key == inner.Name))
                            menu[pname] = inner.Value;
                    }

                    var categoryMap = new Dictionary<string, string>
                    {
                        { "Фильмы", "movie" },
                        { "Сериалы", "tv" },
                        { "Мультфильмы", "cartoons" },
                        { "Аниме", "anime" },
                        { "Релизы", "relise" }
                    };

                    string targetCat, targetName = null;

                    if (categoryMap.TryGetValue(prop.Name, out targetCat))
                        targetName = prop.Name;

                    if (prop.Name.StartsWith("["))
                    {
                        targetCat = prop.Name.Split(']')[0].Trim('[');
                        targetName = prop.Name.Split(']')[1];
                    }

                    if (!string.IsNullOrEmpty(targetName) && !string.IsNullOrEmpty(targetCat))
                    {
                        var targetObj = new JObject();

                        foreach (var inner in catObj.Properties())
                        {
                            if (targetName.Trim() != inner.Name)
                                targetObj[inner.Name] = inner.Value;
                            else
                                targetObj[defaultName ?? inner.Name] = inner.Value;
                        }

                        if (targetObj.HasValues)
                            result[s.key][targetCat.Trim()] = targetObj;
                    }
                }

                if (menu.HasValues)
                    result[s.key]["menu"] = menu;

                if (main.HasValues)
                    result[s.key]["main"] = main;
            }
            #endregion

            return result.ToString(Formatting.None);
        }
    }
}
