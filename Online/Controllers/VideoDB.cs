using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Shared.Engine.Online;

namespace Lampac.Controllers.LITE
{
    public class VideoDB : BaseController
    {
        [HttpGet]
        [Route("lite/videodb")]
        async public Task<ActionResult> Index(int id, long kinopoisk_id, string title, string original_title, int serial, string t, int s = -1, int sid = -1)
        {
            if (!AppInit.conf.VideoDB.enable)
                return Content(string.Empty);

            var oninvk = new VideoDBInvoke
            (
               host,
               (url, head) => HttpClient.Get(url, timeoutSeconds: 8, useproxy: AppInit.conf.VideoDB.useproxy, addHeaders: head),
               streamfile => HostStreamProxy(AppInit.conf.VideoDB.streamproxy, streamfile)
            );

            var content = await InvokeCache($"videodb:view:{kinopoisk_id}", AppInit.conf.multiaccess ? 20 : 5, () => oninvk.Embed(kinopoisk_id, serial));
            if (content.pl == null)
                return Content(string.Empty);

            return Content(oninvk.Html(content, kinopoisk_id, title, original_title, t, s, sid), "text/html; charset=utf-8");
        }
    }
}
