using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Attributes;
using Shared.Models.Base;
using Shared.Models.Templates;
using System.Threading.Tasks;
using System.Web;

namespace BamBoo;

public class BamBooController : BaseOnlineController
{
    public BamBooController() : base(ModInit.conf) { }

    [HttpGet]
    [Staticache]
    [Route("lite/bamboo")]
    async public Task<ActionResult> Index(string title, string original_title, int clarification, int year, int t = -1, string href = null, bool rjson = false, bool similar = false)
    {
        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        var oninvk = new BamBooInvoke
        (
           host,
           init.host,
           httpHydra,
           onstreamtofile => HostStreamProxy(onstreamtofile)
        );

        #region search
        if (string.IsNullOrWhiteSpace(href))
        {
            if (string.IsNullOrWhiteSpace(title ?? original_title))
                return OnError();

            searchFallback:

            string query = (similar || clarification == 1) ? title : original_title;

            var search = await InvokeCacheResult($"bamboo:search:{query}", 60 * 4,
                () => oninvk.Search(query),
                textJson: true
            );

            if (IsRhubFallback(search))
                goto searchFallback;

            return ContentTpl(search, () =>
            {
                if (search.Value.similars == null)
                    return default;

                var stpl = new SimilarTpl(search.Value.similars.Count);

                string enc_title = HttpUtility.UrlEncode(title);
                string enc_original_title = HttpUtility.UrlEncode(original_title);

                foreach (var similar in search.Value.similars)
                {
                    stpl.Append(
                        similar.title,
                        similar.year,
                        string.Empty,
                        $"{host}/lite/bamboo?title={enc_title}&original_title={enc_original_title}&href={HttpUtility.UrlEncode(similar.href)}",
                        PosterApi.Size(similar.img)
                    );
                }

                return stpl;
            });
        }
    #endregion

    rhubFallback:

        var cache = await InvokeCacheResult($"bamboo:view:{href}", 40,
            () => oninvk.Embed(href),
            textJson: true
        );

        if (IsRhubFallback(cache))
            goto rhubFallback;

        return ContentTpl(cache,
            () => oninvk.Tpl(cache.Value, title, original_title, year, t, href, vast: init.vast, rjson: rjson)
        );
    }
}
