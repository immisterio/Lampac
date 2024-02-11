using Lampac.Engine.CORE;
using Shared.Model.Templates;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class iRemuxInvoke
    {
        #region iRemuxInvoke
        string? host;
        string apihost;
        Func<string, ValueTask<string?>> onget;
        Func<string, string, ValueTask<string?>> onpost;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;

        public iRemuxInvoke(string? host, string apihost, Func<string, ValueTask<string?>> onget, Func<string, string, ValueTask<string?>> onpost, Func<string, string> onstreamfile, Func<string, string>? onlog = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.onpost = onpost;
        }
        #endregion

        #region Embed
        async public ValueTask<string?> Embed(string? title, string? original_title, int year)
        {
            string? search = await onpost($"{apihost}/index.php?do=search", $"do=search&subaction=search&from_page=0&story={HttpUtility.UrlEncode(title ?? original_title)}");
            if (search == null)
                return null;

            string? link = null, reservedlink = null;
            foreach (string row in search.Split("class=\"entry\"").Skip(1))
            {
                var g = Regex.Match(row, "class=\"entry__title [^\"]+\"><a href=\"(https?://[^\"]+)\">([^<]+)</a>").Groups;

                if (g[2].Value.ToLower().Contains(title.ToLower()) || (!string.IsNullOrEmpty(original_title) && g[2].Value.ToLower().Contains(original_title.ToLower())))
                {
                    reservedlink = g[1].Value;
                    if (string.IsNullOrEmpty(reservedlink))
                        continue;

                    if (g[2].Value.Contains($"({year}/"))
                    {
                        link = reservedlink;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(link))
            {
                if (string.IsNullOrEmpty(reservedlink))
                    return null;

                link = reservedlink;
            }

            string? news = await onget(link);
            if (news == null)
                return null;

            string content = news.Split("id=\"msg\"")[1].Split("id=\"download")[0];
            if (!content.Contains("cloud.mail.ru/public/"))
                return null;

            return content;
        }
        #endregion

        #region Html
        public string Html(string? content, string? title, string? original_title)
        {
            if (content == null)
                return string.Empty;

            var mtpl = new MovieTpl(title, original_title);

            string linkid = Regex.Match(content, "1080p(<br>)?<a href=\"https?://cloud.mail.ru/public/([^\"]+)\"").Groups[2].Value;
            if (!string.IsNullOrEmpty(linkid))
                mtpl.Append("1080p", host + $"lite/remux/movie?linkid={linkid}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}", "call");

            return mtpl.ToHtml();
        }
        #endregion


        #region Weblink
        async public ValueTask<string?> Weblink(string linkid)
        {
            string? html = await onget($"https://cloud.mail.ru/public/{linkid}");
            if (html == null)
                return null;

            string? weblinkRow = StringConvert.FindLastText(html, "\"weblink_get\"", "}");
            if (weblinkRow == null)
                return null;

            string location = Regex.Match(weblinkRow, "\"url\": ?\"(https?://[^/]+)").Groups[1].Value;
            if (string.IsNullOrEmpty(location))
                return null;

            return $"{location}/weblink/view/{linkid}";
        }
        #endregion

        #region Movie
        public string Movie(string weblink, string title, string original_title)
        {
            string lnk = onstreamfile?.Invoke(weblink);
            return "{\"method\":\"play\",\"url\":\"" + lnk + "\",\"title\":\"" + (title ?? original_title) + "\", \"quality\": {\"1080p\":\""+lnk+ "\"}, \"subtitles\": []}";
        }
        #endregion
    }
}
