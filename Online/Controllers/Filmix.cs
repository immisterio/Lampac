using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Newtonsoft.Json.Linq;
using Shared.Engine.Online;

namespace Lampac.Controllers.LITE
{
    public class Filmix : BaseController
    {
        #region filmixpro
        [HttpGet]
        [Route("lite/filmixpro")]
        async public Task<ActionResult> Pro()
        {
            var token_request = await HttpClient.Get<JObject>($"{AppInit.conf.Filmix.host}/api/v2/token_request?user_dev_apk=2.0.1&user_dev_id=&user_dev_name=Xiaomi&user_dev_os=11&user_dev_vendor=Xiaomi&user_dev_token=");

            string html = "1. Откройте <a href='https://filmix.ac/consoles'>https://filmix.ac/consoles</a> <br>";
            html += $"2. Введите код <b>{token_request.Value<string>("user_code")}</b><br>";
            html += $"<br><br>В init.conf<br>";
            html += $"1. Укажите token <b>{token_request.Value<string>("code")}</b><br>";
            html += $"2. Измените \"pro\": false, на \"pro\": true, если у вас PRO аккаунт</b>";

            return Content(html, "text/html; charset=utf-8");
        }
        #endregion

        [HttpGet]
        [Route("lite/filmix")]
        async public Task<ActionResult> Index(string title, string original_title, int clarification, string original_language, int year, int postid, int t, int s = -1)
        {
            if (!AppInit.conf.Filmix.enable)
                return Content(string.Empty);

            if (original_language != "en")
                clarification = 1;

            var oninvk = new FilmixInvoke
            (
               host,
               AppInit.conf.Filmix.host,
               AppInit.conf.Filmix.token,
               ongettourl => HttpClient.Get(ongettourl, timeoutSeconds: 8),
               onstreamtofile => HostStreamProxy(AppInit.conf.Filmix.streamproxy, onstreamtofile)
            );

            if (postid == 0)
            {
                var res = await InvokeCache($"filmix:search:{title}:{original_title}:{clarification}", AppInit.conf.multiaccess ? 40 : 10, () => oninvk.Search(title, original_title, clarification, year));
                if (res.id == 0)
                    return Content(res.similars);

                postid = res.id;
            }

            var player_links = await InvokeCache($"filmix:post:{postid}", AppInit.conf.multiaccess ? 20 : 5, () => oninvk.Post(postid));
            if (player_links == null)
                return Content(string.Empty);

            return Content(oninvk.Html(player_links, AppInit.conf.Filmix.pro, postid, title, original_title, t, s), "text/html; charset=utf-8");
        }
    }
}
