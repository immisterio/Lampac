using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Tizam
{
    public class ViewController : BaseSisiController
    {
        public ViewController() : base(AppInit.conf.Tizam) { }

        [Route("tizam/vidosik")]
        async public ValueTask<ActionResult> Index(string uri)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            rhubFallback:
            var cache = await InvokeCacheResult<StreamItem>($"tizam:view:{uri}", 180, async e =>
            {
                string html = await httpHydra.Get($"{init.corsHost()}/{uri}");

                string location = Regex.Match(html ?? string.Empty, "src=\"(https?://[^\"]+\\.mp4)\" type=\"video/mp4\"").Groups[1].Value;

                if (string.IsNullOrEmpty(location))
                    return e.Fail("location", refresh_proxy: true);

                return e.Success(new StreamItem()
                {
                    qualitys = new Dictionary<string, string>()
                    {
                        ["auto"] = location
                    }
                });
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return OnResult(cache);
        }
    }
}
