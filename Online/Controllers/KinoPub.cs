using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Newtonsoft.Json.Linq;
using Online;
using Shared.Engine.CORE;
using Shared.Engine.Online;

namespace Lampac.Controllers.LITE
{
    public class KinoPub : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager("kinopub", AppInit.conf.KinoPub);

        #region kinopubpro
        [HttpGet]
        [Route("lite/kinopubpro")]
        async public Task<ActionResult> Pro(string code, string name)
        {
            var proxy = proxyManager.Get();
            var init = AppInit.conf.KinoPub;

            if (string.IsNullOrWhiteSpace(code))
            {
                var token_request = await HttpClient.Post<JObject>($"{init.corsHost()}/oauth2/device?grant_type=device_code&client_id=xbmc&client_secret=cgg3gtifu46urtfp2zp1nqtba0k2ezxh", "", proxy: proxy);

                string html = "1. Откройте <a href='https://kino.pub/device'>https://kino.pub/device</a> <br>";
                html += $"2. Введите код активации <b>{token_request.Value<string>("user_code")}</b><br>";
                html += $"3. Когда на сайте kino.pub появится \"Ожидание устройства\", нажмите кнопку \"Проверить активацию\" которая ниже</b>";

                html += $"<br><br><a href='/lite/kinopubpro?code={token_request.Value<string>("code")}&name={name}'><button>Проверить активацию</button></a>";

                return Content(html, "text/html; charset=utf-8");
            }
            else
            {
                var device_token = await HttpClient.Post<JObject>($"{init.corsHost()}/oauth2/device?grant_type=device_token&client_id=xbmc&client_secret=cgg3gtifu46urtfp2zp1nqtba0k2ezxh&code={code}", "", proxy: proxy);
                if (device_token == null || string.IsNullOrWhiteSpace(device_token.Value<string>("access_token")))
                    return LocalRedirect("/lite/kinopubpro");

                if (!string.IsNullOrEmpty(name))
                    await HttpClient.Post($"{init.corsHost()}/v1/device/notify?access_token={device_token.Value<string>("access_token")}", $"&title={name}", proxy: proxy);

                return Content($"В init.conf укажите token <b>{device_token.Value<string>("access_token")}</b>", "text/html; charset=utf-8");
            }
        }
        #endregion

        [HttpGet]
        [Route("lite/kinopub")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int year, int clarification, string original_language, int postid, int s = -1)
        {
            var init = AppInit.conf.KinoPub;

            if (!init.enable)
                return OnError();

            var proxy = proxyManager.Get();

            var oninvk = new KinoPubInvoke
            (
               host,
               init.corsHost(),
               init.token,
               ongettourl => HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy),
               (stream, filepath) => HostStreamProxy(init, stream, proxy: proxy)
            );

            if (postid == 0)
            {
                if (original_language != "en")
                    clarification = 1;

                var res = await InvokeCache($"kinopub:search:{title}:{clarification}:{imdb_id}", cacheTime(40), () => oninvk.Search(title, original_title, year, clarification, imdb_id, kinopoisk_id));

                if (res?.similars != null)
                    return Content(res.similars, "text/html; charset=utf-8");

                postid = res == null ? 0 : res.id;

                if (postid == 0)
                    return OnError(proxyManager);

                if (postid == -1)
                    return OnError();
            }

            var root = await InvokeCache($"kinopub:post:{postid}", cacheTime(10), () => oninvk.Post(postid));
            if (root == null)
                return OnError(proxyManager);

            return Content(oninvk.Html(root, init.filetype, title, original_title, postid, s), "text/html; charset=utf-8");
        }
    }
}
