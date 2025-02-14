using Shared.Model.Base;
using Shared.Model.Online.Redheadsound;
using Shared.Model.Templates;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class RedheadsoundInvoke
    {
        #region RedheadsoundInvoke
        string? host;
        string apihost;
        Func<string, ValueTask<string?>> onget;
        Func<string, string, ValueTask<string?>> onpost;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;
        Action? requesterror;

        public RedheadsoundInvoke(string? host, string apihost, Func<string, ValueTask<string?>> onget, Func<string, string, ValueTask<string?>> onpost, Func<string, string> onstreamfile, Func<string, string>? onlog = null, Action? requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.onpost = onpost;
            this.requesterror = requesterror;
        }
        #endregion

        #region Embed
        async public ValueTask<EmbedModel?> Embed(string? title, int year)
        {
            if (string.IsNullOrEmpty(title))
                return null;

            string? search = await onpost($"{apihost}/index.php?do=search", $"do=search&subaction=search&search_start=0&full_search=0&result_from=1&story={HttpUtility.UrlEncode(title)}");
            if (search == null)
            {
                requesterror?.Invoke();
                return null;
            }

            string? link = null, reservedlink = null;
            foreach (string row in search.Split("card d-flex").Skip(1))
            {
                if (row.ToLower().Contains($">{title.ToLower()}<"))
                {
                    string rlnk = Regex.Match(row, "href=\"(https?://[^/]+/[^\"]+\\.html)\"").Groups[1].Value;
                    if (string.IsNullOrWhiteSpace(rlnk))
                        continue;

                    reservedlink = rlnk;

                    if (Regex.Match(row, "<span>Год выпуска:</span> ?<a [^>]+>([0-9]{4})</a>").Groups[1].Value == year.ToString())
                    {
                        link = reservedlink;
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(link))
            {
                if (string.IsNullOrWhiteSpace(reservedlink))
                {
                    if (search.Contains(">Поиск по сайту<"))
                        return new EmbedModel() { IsEmpty = true };

                    return null;
                }

                link = reservedlink;
            }

            string? news = await onget(link);
            if (news == null)
            {
                requesterror?.Invoke();
                return null;
            }

            string iframeUri = Regex.Match(news, "<iframe data-src=\"(https?://[^\"]+)\"").Groups[1].Value;
            if (string.IsNullOrWhiteSpace(iframeUri))
                return null;

            string? iframe = await onget(iframeUri);
            if (string.IsNullOrWhiteSpace(iframe) || !iframe.Contains("sources:"))
            {
                requesterror?.Invoke();
                return null;
            }

            return new EmbedModel() { iframe = iframe.Split("sources:")[1].Split("poster")[0], iframeUri = iframeUri };
        }
        #endregion

        #region Html
        public string Html(EmbedModel? content, string? title, VastConf? vast = null, bool rjson = false)
        {
            if (content == null || content.IsEmpty)
                return string.Empty;

            var mtpl = new MovieTpl(title, null);

            string quality = content.iframe.Contains("1080p") ? "1080p": content.iframe.Contains("720p") ? "720p" : "360p";
            string hls = Regex.Match(content.iframe, "\"src\":\"([^\"]+)\"").Groups[1].Value;
            if (!string.IsNullOrEmpty(hls))
                mtpl.Append(quality, onstreamfile(hls.Replace("\u0026", "&")));

            return rjson ? mtpl.ToJson() : mtpl.ToHtml();
        }
        #endregion
    }
}
