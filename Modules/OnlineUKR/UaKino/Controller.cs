using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Attributes;
using Shared.Models.Base;
using Shared.Models.Templates;
using System;
using System.Threading.Tasks;
using System.Web;

namespace UaKino;

public class UaKinoController : BaseOnlineController
{
    public UaKinoController() : base(ModInit.conf) { }

    [HttpGet]
    [Staticache]
    [Route("lite/uakino")]
    async public Task<ActionResult> Index(string title, string original_title, int clarification, int year, string href = null, bool rjson = false, bool similar = false, string source = null, string id = null)
    {
        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
        {
            if (source.Equals("uakino", StringComparison.OrdinalIgnoreCase))
                href = id;
        }

        var oninvk = new UaKinoInvoke
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

            string query = (similar || clarification == 1) ? title : original_title;

            var search = await InvokeCacheResult($"uakino:search:{query}", 240,
                () => oninvk.Search(query)
            );

            if (IsRhubFallback(search))
                goto searchFallback;

            if (search.Value?.similars == null || search.Value.similars.Count == 0)
                return OnError();

            return ContentTpl(search, () =>
            {
                var stpl = new SimilarTpl(search.Value.similars.Count);

                string enc_title = HttpUtility.UrlEncode(title);
                string enc_original_title = HttpUtility.UrlEncode(original_title);

                foreach (var item in search.Value.similars)
                {
                    stpl.Append(
                        item.title,
                        string.Empty,
                        string.Empty,
                        $"{host}/lite/uakino?title={enc_title}&original_title={enc_original_title}&href={HttpUtility.UrlEncode(item.href)}",
                        PosterApi.Size(item.img)
                    );
                }

                return stpl;
            });
        }
    #endregion

    rhubFallback:

        var cache = await InvokeCacheResult($"uakino:view:{href}", 240,
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
