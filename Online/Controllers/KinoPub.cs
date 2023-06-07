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
        async public Task<ActionResult> Pro(string code)
        {
            var proxy = proxyManager.Get();

            if (string.IsNullOrWhiteSpace(code))
            {
                var token_request = await HttpClient.Post<JObject>($"{AppInit.conf.KinoPub.corsHost()}/oauth2/device?grant_type=device_code&client_id=xbmc&client_secret=cgg3gtifu46urtfp2zp1nqtba0k2ezxh", "", proxy: proxy);

                string html = "1. Откройте <a href='https://kino.pub/device'>https://kino.pub/device</a> <br>";
                html += $"2. Введите код активации <b>{token_request.Value<string>("user_code")}</b><br>";
                html += $"3. Когда на сайте kino.pub появится \"Ожидание устройства\", нажмите кнопку \"Проверить активацию\" которая ниже</b>";

                html += $"<br><br><a href='/lite/kinopubpro?code={token_request.Value<string>("code")}'><button>Проверить активацию</button></a>";

                return Content(html, "text/html; charset=utf-8");
            }
            else
            {
                var device_token = await HttpClient.Post<JObject>($"{AppInit.conf.KinoPub.corsHost()}/oauth2/device?grant_type=device_token&client_id=xbmc&client_secret=cgg3gtifu46urtfp2zp1nqtba0k2ezxh&code={code}", "", proxy: proxy);
                if (device_token == null || string.IsNullOrWhiteSpace(device_token.Value<string>("access_token")))
                    return LocalRedirect("/lite/kinopubpro");

                await HttpClient.Post($"{AppInit.conf.KinoPub.corsHost()}/v1/device/notify?access_token={device_token.Value<string>("access_token")}", "&title=LAMPAC", proxy: proxy);

                return Content($"В init.conf укажите token <b>{device_token.Value<string>("access_token")}</b>", "text/html; charset=utf-8");
            }
        }
        #endregion

        [HttpGet]
        [Route("lite/kinopub")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int clarification, string original_language, int postid, int s = -1)
        {
            if (string.IsNullOrWhiteSpace(AppInit.conf.KinoPub.token))
                return OnError();

            var proxy = proxyManager.Get();

            var oninvk = new KinoPubInvoke
            (
               host,
               AppInit.conf.KinoPub.corsHost(),
               AppInit.conf.KinoPub.token,
               ongettourl => HttpClient.Get(AppInit.conf.KinoPub.corsHost(ongettourl), timeoutSeconds: 8, proxy: proxy),
               onstreamtofile => HostStreamProxy(AppInit.conf.KinoPub, onstreamtofile, proxy: proxy)
            );

            if (postid == 0)
            {
                if (kinopoisk_id == 0 && string.IsNullOrEmpty(imdb_id))
                    return OnError();

                if (original_language != "en")
                    clarification = 1;

                postid = await InvokeCache($"kinopub:search:{title}:{clarification}:{imdb_id}", AppInit.conf.multiaccess ? 40 : 10, () => oninvk.Search(title, original_title, clarification, imdb_id, kinopoisk_id));

                if (postid == 0)
                    return OnError(proxyManager);

                if (postid == -1)
                    return OnError();
            }

            var root = await InvokeCache($"kinopub:post:{postid}", AppInit.conf.multiaccess ? 10 : 5, () => oninvk.Post(postid));
            if (root == null)
                return OnError(proxyManager);

            return Content(oninvk.Html(root, AppInit.conf.KinoPub.filetype, title, original_title, postid, s), "text/html; charset=utf-8");
        }
    }
}
