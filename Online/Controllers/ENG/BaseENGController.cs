using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Online;
using Shared.Model.Templates;
using Shared.Engine;
using Lampac.Models.LITE;
using Newtonsoft.Json.Linq;
using Lampac.Engine.CORE;
using System.Web;

namespace Lampac.Controllers.LITE
{
    public class BaseENGController : BaseOnlineController
    {
        async public Task<ActionResult> ViewTmdb(OnlinesSettings init, bool browser, bool checksearch, long id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false, bool mp4 = false)
        {
            if (checksearch)
                return Content("data-json=");

            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (browser && Firefox.Status == PlaywrightStatus.disabled)
                return OnError();

            if (serial == 1)
            {
                #region Сериал
                var tmdb = await InvokeCache<JToken>($"tmdb:seasons:{id}", cacheTime(40, init: init), async res =>
                {
                    var root = await HttpClient.Get<JObject>($"{AppInit.conf.cub.scheme}://tmdb.{AppInit.conf.cub.mirror}/3/tv/{id}?api_key={AppInit.conf.tmdb.api_key}");

                    if (root == null || !root.ContainsKey("seasons"))
                        return res.Fail("seasons");

                    return root["seasons"];
                });

                if (!tmdb.IsSuccess)
                    return OnError(tmdb.ErrorMsg);

                if (s == -1)
                {
                    #region Сезоны
                    var tpl = new SeasonTpl();

                    foreach (var season in tmdb.Value)
                    {
                        int number = season.Value<int>("season_number");
                        if (1 > number)
                            continue;

                        string link = $"{host}/lite/{init.plugin.ToLower()}?id={id}&imdb_id={imdb_id}&serial=1&rjson={rjson}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={number}";
                        tpl.Append($"{number} сезон", link, number);
                    }

                    return ContentTo(rjson ? tpl.ToJson() : tpl.ToHtml());
                    #endregion
                }
                else
                {
                    #region Серии
                    var etpl = new EpisodeTpl();

                    foreach (var season in tmdb.Value)
                    {
                        if (season.Value<int>("season_number") != s)
                            continue;

                        for (int i = 1; i <= season.Value<int>("episode_count"); i++)
                        {
                            string path = mp4 ? "video" : "video.m3u8";
                            etpl.Append($"{i} серия", title ?? original_title, s.ToString(), i.ToString(), accsArgs($"{host}/lite/{init.plugin.ToLower()}/{path}?id={id}&imdb_id={imdb_id}&s={s}&e={i}"), vast: init.vast);
                        }
                    }

                    return ContentTo(rjson ? etpl.ToJson() : etpl.ToHtml());
                    #endregion
                }
                #endregion
            }
            else
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title);

                string path = mp4 ? "video" : "video.m3u8";
                mtpl.Append("English", accsArgs($"{host}/lite/{init.plugin.ToLower()}/{path}?id={id}&imdb_id={imdb_id}"), vast: init.vast);

                return ContentTo(rjson ? mtpl.ToJson() : mtpl.ToHtml());
                #endregion
            }
        }
    }
}
