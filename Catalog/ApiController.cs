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
                    if (init == null || !init.enable || init.menu == null)
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
                                    siteObj["search"] = $"{host}/catalog/list?plugin={HttpUtility.UrlEncode(site)}";

                                siteObj["search_lazy"] = init.search_lazy;

                                if (!string.IsNullOrEmpty(init.catalog_key))
                                    siteObj["catalog_key"] = init.catalog_key;

                                if (!string.IsNullOrEmpty(menuItem.defaultName))
                                    siteObj["defaultName"] = menuItem.defaultName;

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
                var movie = new JObject();
                var tv = new JObject();
                var anime = new JObject();
                var cartoons = new JObject();

                foreach (var prop in s.obj.Properties())
                {
                    if (prop.Name == "search" || prop.Name == "catalog_key" || prop.Name == "defaultName")
                        continue;

                    if (!(prop.Value is JObject catObj))
                        continue;

                    foreach (var inner in catObj.Properties())
                    {
                        if (prop.Name != inner.Name)
                            main[$"{prop.Name} • {inner.Name.ToLower()}"] = inner.Value;
                        else
                            main[prop.Name] = inner.Value;

                        if (!menu.ContainsKey(prop.Name) || (catalog_key != null && catalog_key == inner.Name))
                            menu[prop.Name] = inner.Value;
                    }

                    if (prop.Name == "Фильмы")
                    {
                        foreach (var inner in catObj.Properties())
                        {
                            if (prop.Name != inner.Name)
                                movie[inner.Name] = inner.Value;
                            else
                                movie[defaultName ?? inner.Name] = inner.Value;
                        }
                    }

                    if (prop.Name == "Сериалы")
                    {
                        foreach (var inner in catObj.Properties())
                        {
                            if (prop.Name != inner.Name)
                                tv[inner.Name] = inner.Value;
                            else
                                tv[defaultName ?? inner.Name] = inner.Value;
                        }
                    }

                    if (prop.Name == "Мультфильмы")
                    {
                        foreach (var inner in catObj.Properties())
                        {
                            if (prop.Name != inner.Name)
                                cartoons[inner.Name] = inner.Value;
                            else
                                cartoons[defaultName ?? inner.Name] = inner.Value;
                        }
                    }

                    if (prop.Name == "Аниме")
                    {
                        foreach (var inner in catObj.Properties())
                        {
                            if (prop.Name != inner.Name)
                                anime[inner.Name] = inner.Value;
                            else
                                anime[defaultName ?? inner.Name] = inner.Value;
                        }
                    }
                }

                if (menu.HasValues)
                    result[s.key]["menu"] = menu;

                if (main.HasValues)
                    result[s.key]["main"] = main;

                if (movie.HasValues)
                    result[s.key]["movie"] = movie;

                if (tv.HasValues)
                    result[s.key]["tv"] = tv;

                if (cartoons.HasValues)
                    result[s.key]["cartoons"] = cartoons;

                if (anime.HasValues)
                    result[s.key]["anime"] = anime;
            }
            #endregion

            return result.ToString(Formatting.None);
        }
    }
}
