using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Attributes;
using Shared.Models.Base;
using Shared.Models.Templates;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Eneyida;

public class EneyidaController : BaseOnlineController
{
    public EneyidaController() : base(ModInit.conf) { }

    [HttpGet]
    [Staticache]
    [Route("lite/eneyida")]
    async public Task<ActionResult> Index(string title, string original_title, int clarification, int year, string href = null, bool rjson = false, bool similar = false, string source = null, string id = null)
    {
        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
        {
            if (source.Equals("eneyida", StringComparison.OrdinalIgnoreCase))
                href = id;
        }

        var oninvk = new EneyidaInvoke
        (
           init.host,
           httpHydra
        );

        #region search
        if (string.IsNullOrWhiteSpace(href))
        {
            if (string.IsNullOrWhiteSpace(title ?? original_title))
                return OnError();

            searchFallback:

            string _y = year.ToString();
            string query = (similar || clarification == 1) ? title : original_title;

            var search = await InvokeCacheResult($"eneyida:search:{query}", 240,
                () => oninvk.Search(query)
            );

            if (IsRhubFallback(search))
                goto searchFallback;

            if (search.Value?.similars == null || search.Value.similars.Count == 0)
                return OnError();

            if (similar || search.Value.similars.FirstOrDefault(i => i.year == _y) == null)
            {
                return ContentTpl(search, () =>
                {
                    var stpl = new SimilarTpl(search.Value.similars.Count);

                    string enc_title = HttpUtility.UrlEncode(title);
                    string enc_original_title = HttpUtility.UrlEncode(original_title);

                    foreach (var similar in search.Value.similars)
                    {
                        stpl.Append(
                            similar.title,
                            similar.year,
                            string.Empty,
                            $"{host}/lite/eneyida?title={enc_title}&original_title={enc_original_title}&href={HttpUtility.UrlEncode(similar.href)}",
                            PosterApi.Size(similar.img)
                        );
                    }

                    return stpl;
                });
            }

            href = search.Value.similars.FirstOrDefault(i => i.year == _y).href;
        }
    #endregion

    rhubFallback:

        var cache = await InvokeCacheResult($"eneyida:view:{href}", 240,
            () => oninvk.Embed(href)
        );

        if (IsRhubFallback(cache))
            goto rhubFallback;

        if (string.IsNullOrEmpty(cache.Value))
            return OnError();

        string args = $"?uri={EncryptQuery(cache.Value)}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&rjson={rjson}";

        return LocalRedirect(accsArgs("/lite/hdvbua" + args));
    }
}
