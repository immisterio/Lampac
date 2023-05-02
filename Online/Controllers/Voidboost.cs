using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Shared.Engine.Online;

namespace Lampac.Controllers.LITE
{
    public class Voidboost : BaseController
    {
        #region VoidboostInvoke
        static VoidboostInvoke oninvk;

        public Voidboost()
        {
            oninvk = new VoidboostInvoke
            (
                host,
                AppInit.conf.Voidboost.corsHost(),
                ongettourl => HttpClient.Get(AppInit.conf.Voidboost.corsHost(ongettourl), timeoutSeconds: 8),
                (url, data) => HttpClient.Post(AppInit.conf.Voidboost.corsHost(url), data, timeoutSeconds: 8),
                streamfile => HostStreamProxy(AppInit.conf.Voidboost.streamproxy, streamfile)
            );
        }
        #endregion

        [HttpGet]
        [Route("lite/voidboost")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, string t)
        {
            if (!AppInit.conf.Voidboost.enable)
                return Content(string.Empty);

            var content = await InvokeCache($"voidboost:view:{kinopoisk_id}:{imdb_id}:{t}", AppInit.conf.multiaccess ? 20 : 10, () => oninvk.Embed(imdb_id, kinopoisk_id, t));
            if (content == null)
                return Content(string.Empty);

            return Content(oninvk.Html(content, imdb_id, kinopoisk_id, title, original_title, t), "text/html; charset=utf-8");
        }


        #region Serial
        [HttpGet]
        [Route("lite/voidboost/serial")]
        async public Task<ActionResult> Serial(string imdb_id, long kinopoisk_id, string title, string original_title, string t, int s)
        {
            if (!AppInit.conf.Voidboost.enable)
                return Content(string.Empty);

            string html = await InvokeCache($"voidboost:view:serial:{t}:{s}", AppInit.conf.multiaccess ? 20 : 10, () => oninvk.Serial(imdb_id, kinopoisk_id, title, original_title, t, s, true));
            if (html == null)
                return Content(string.Empty);

            return Content(html + "</div>", "text/html; charset=utf-8");
        }
        #endregion

        #region Movie / Episode
        [HttpGet]
        [Route("lite/voidboost/movie")]
        [Route("lite/voidboost/episode")]
        async public Task<ActionResult> Movie(string title, string original_title, string t, int s, int e, bool play)
        {
            if (!AppInit.conf.Voidboost.enable)
                return Content(string.Empty);

            string result = await InvokeCache($"rezka:view:stream:{t}:{s}:{e}", AppInit.conf.multiaccess ? 20 : 10, () => oninvk.Movie(title, original_title, t, s, e, play));
            if (result == null)
                return Content(string.Empty);

            if (play)
                return Redirect(result);

            return Content(result);
        }
        #endregion
    }
}
