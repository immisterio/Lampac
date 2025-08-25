using Shared.Models.Base;
using Shared.Models.Online.Redheadsound;
using Shared.Models.Templates;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public struct RedheadsoundInvoke
    {
        #region RedheadsoundInvoke
        string host;
        string apihost;
        Func<string, ValueTask<string>> onget;
        Func<string, string, ValueTask<string>> onpost;
        Func<string, string> onstreamfile;
        Func<string, string> onlog;
        Action requesterror;

        public RedheadsoundInvoke(string host, string apihost, Func<string, ValueTask<string>> onget, Func<string, string, ValueTask<string>> onpost, Func<string, string> onstreamfile, Func<string, string> onlog = null, Action requesterror = null)
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
        async public Task<EmbedModel> Embed(string title, int year)
        {
            if (string.IsNullOrEmpty(title))
                return null;

            string search = await onpost($"{apihost}/index.php?do=search", $"do=search&subaction=search&search_start=0&full_search=0&result_from=1&story={HttpUtility.UrlEncode(title)}");
            if (search == null)
            {
                requesterror?.Invoke();
                return null;
            }

            string link = null, reservedlink = null;
            foreach (string row in search.Split("card d-flex").Skip(1))
            {
                if (StringConvert.SearchName(row).Contains(StringConvert.SearchName(title)))
                {
                    string rlnk = Regex.Match(row, "href=\"(https?://[^/]+/[^\"]+\\.html)\"").Groups[1].Value;
                    if (string.IsNullOrEmpty(rlnk))
                        continue;

                    reservedlink = rlnk;

                    if (Regex.Match(row, "<span>Год выпуска:</span> ?<a [^>]+>([0-9]{4})</a>").Groups[1].Value == year.ToString())
                    {
                        link = reservedlink;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(link))
            {
                if (string.IsNullOrEmpty(reservedlink))
                {
                    if (search.Contains(">Поиск по сайту<"))
                        return new EmbedModel() { IsEmpty = true };

                    return null;
                }

                link = reservedlink;
            }

            string news = await onget(link);
            if (news == null)
            {
                requesterror?.Invoke();
                return null;
            }

            string iframeUri = Regex.Match(news, "url:([\t ]+)?(\"|')(?<uri>https?://[^\'\"\n\r\t ]+)").Groups["uri"].Value;
            if (string.IsNullOrWhiteSpace(iframeUri))
                return null;

            string iframe = await onget(iframeUri);
            if (string.IsNullOrEmpty(iframe))
                return null;

            string contentUrl = Regex.Match(iframe, "\"contentUrl\": ?\"([^\"]+)\"").Groups[1].Value;
            if (string.IsNullOrEmpty(contentUrl))
                return null;

            return new EmbedModel() { iframe = contentUrl };
        }
        #endregion

        #region Html
        public string Html(EmbedModel content, string title, VastConf vast = null, bool rjson = false)
        {
            if (content == null || content.IsEmpty)
                return string.Empty;

            var mtpl = new MovieTpl(title, null, 1);

            mtpl.Append("1080p", onstreamfile(content.iframe.Replace("&amp;", "&")));

            return rjson ? mtpl.ToJson() : mtpl.ToHtml();
        }
        #endregion
    }
}
