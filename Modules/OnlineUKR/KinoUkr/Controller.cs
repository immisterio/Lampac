using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Attributes;
using Shared.Models.Templates;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace KinoUkr;

public class KinoUkrController : BaseOnlineController
{
    public KinoUkrController() : base(ModInit.conf) { }

    [HttpGet]
    [Staticache]
    [Route("lite/kinoukr")]
    async public Task<ActionResult> Index(string iframe, string imdb_id, long kinopoisk_id, string title, string original_title, int year, bool similar = false, string source = null, string id = null)
    {
        iframe = DecryptQuery(iframe);

        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        var oninvk = new KinoukrInvoke(httpHydra);

        if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
        {
            if (source.Equals("kinoukr", StringComparison.OrdinalIgnoreCase))
            {
            rhubFallback:

                var cache = await InvokeCacheResult($"kinoukr:view:{id}", 240,
                    () => oninvk.Embed($"{init.host}/{id}")
                );

                if (IsRhubFallback(cache))
                    goto rhubFallback;

                if (string.IsNullOrEmpty(cache.Value))
                    return OnError();

                iframe = cache.Value;
            }
        }

        if (string.IsNullOrWhiteSpace(iframe))
        {
            if (string.IsNullOrWhiteSpace(title ?? original_title ?? imdb_id) && kinopoisk_id == 0)
                return OnError();

            var search = oninvk.Search(title, original_title, imdb_id, kinopoisk_id);
            if (search?.similars == null || search.similars.Count == 0 || search.IsEmpty)
                return OnError();

            string _y = year.ToString();

            if (similar || search.similars.FirstOrDefault(i => i.year == _y) == null)
            {
                var stpl = new SimilarTpl(search.similars.Count);

                string enc_title = HttpUtility.UrlEncode(title);
                string enc_original_title = HttpUtility.UrlEncode(original_title);

                foreach (var s in search.similars)
                {
                    stpl.Append(
                        s.title,
                        s.year,
                        string.Empty,
                        $"{host}/lite/kinoukr?title={enc_title}&original_title={enc_original_title}&iframe={EncryptQuery(s.href)}"
                    );
                }

                return ContentTpl(stpl);
            }

            iframe = search.similars.FirstOrDefault(i => i.year == _y).href;
        }

        string target = iframe.Contains("tortuga")
            ? "tortuga"
            : "ashdi";


        string args = $"?uri={EncryptQuery(iframe)}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}";

        return LocalRedirect(accsArgs($"/lite/{target}" + args));
    }
}
