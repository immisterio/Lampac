using Shared.Services;
using Shared.Services.RxEnumerate;
using Shared.Services.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KinoUkr;

public struct KinoukrInvoke
{
    #region KinoukrInvoke
    HttpHydra http;

    public KinoukrInvoke(HttpHydra httpHydra)
    {
        http = httpHydra;
    }
    #endregion

    #region Search
    public EmbedModel Search(string title, string original_title, string imdb_id, long kinopoisk_id, bool similar)
    {
        string kp = (kinopoisk_id > 0 ? kinopoisk_id.ToString() : null);
        if (string.IsNullOrEmpty(title ?? original_title ?? kp ?? imdb_id))
            return null;

        var result = new EmbedModel
        {
            similars = new List<Similar>()
        };

        foreach (var item in SearchDb(title, original_title, kp, imdb_id, similar))
        {
            result.similars.Add(new Similar()
            {
                href = !string.IsNullOrEmpty(item.tortuga) ? $"https://tortuga.tw/{item.tortuga}" : $"https://ashdi.vip/{item.ashdi}",
                title = $"{item.name} / {item.eng_name}",
                year = item.year
            });
        }

        if (result.similars.Count == 0)
            return new EmbedModel() { IsEmpty = true };

        return result;
    }
    #endregion

    #region SearchDb
    static IEnumerable<DbModel> SearchDb(string name, string eng_name, string kp, string imdb, bool similar)
    {
        if (!string.IsNullOrEmpty(kp) || !string.IsNullOrEmpty(imdb))
        {
            var resultId = ModInit.database.Where(i =>
            {
                if (!string.IsNullOrEmpty(kp) && i.Value.kp_id == kp)
                    return true;

                if (!string.IsNullOrEmpty(imdb) && i.Value.imdb_id == imdb)
                    return true;

                return false;
            });

            if (resultId.Any())
                return resultId.Select(i => i.Value);
        }

        string sname = SearchNameTo.Convert(name);
        string seng_name = SearchNameTo.Convert(eng_name);

        var result = ModInit.database.Where(i =>
        {
            if (similar)
            {
                if (SearchNameTo.Contains(i.Value.name, sname) ||
                    SearchNameTo.Contains(i.Value.eng_name, seng_name))
                    return true;
            }
            else
            {
                if (SearchNameTo.Equals(i.Value.name, sname) ||
                    SearchNameTo.Equals(i.Value.eng_name, seng_name))
                    return true;
            }

            return false;
        });

        return result.Select(i => i.Value);
    }
    #endregion

    #region Embed
    public async Task<string> Embed(string href)
    {
        string iframeUri = null;

        await http.GetSpan(href, news =>
        {
            iframeUri = Rx.Match(news, "src=\"(https?://tortuga\\.[a-z]+/[^\"]+)\"");
            if (string.IsNullOrEmpty(iframeUri))
                iframeUri = Rx.Match(news, "src=\"(https?://ashdi\\.vip/[^\"]+)\"");
        });

        if (string.IsNullOrEmpty(iframeUri))
            return null;

        return iframeUri;
    }
    #endregion
}
