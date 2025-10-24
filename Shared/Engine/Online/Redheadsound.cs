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

            string mainHtml = await onget(apihost);
            string user_hash = Regex.Match(mainHtml ?? "", "var dle_login_hash([\t ]+)?=([\t ]+)?'(?<hash>[a-f0-9]+)'").Groups["hash"].Value;
            if (string.IsNullOrEmpty(user_hash))
                return null;

            string search = await onpost($"{apihost}/engine/ajax/controller.php?mod=search", $"query={HttpUtility.UrlEncode(title)}&skin=rhs_new&user_hash={user_hash}");
            if (search == null)
            {
                requesterror?.Invoke();
                return null;
            }

            string link = null, reservedlink = null;
            foreach (var node in HtmlParse.Nodes(search, "//div[@class='move-item']"))
            {
                string rowTitle = StringConvert.SearchName(node.SelectText(".//h4[@class='title']//a"));
                if (rowTitle == null)
                    continue;

                if (rowTitle.Contains(StringConvert.SearchName(title)))
                {
                    string rlnk = node.SelectText(".//a[@class='move-item__img']", "href");
                    if (rlnk == null)
                        continue;

                    reservedlink = rlnk;

                    if (node.SelectText(".//span[contains(@class, 'year')]//a") == year.ToString())
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
                    if (search.Contains("notfound"))
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

            string iframeUri = Regex.Match(news, "videoUrl([\t ]+)?=([\t ]+)?'(?<uri>https?://[^']+)'").Groups["uri"].Value;
            if (string.IsNullOrEmpty(iframeUri))
                return null;

            string iframe = await onget(iframeUri);
            if (string.IsNullOrEmpty(iframe))
            {
                requesterror?.Invoke();
                return null;
            }

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
