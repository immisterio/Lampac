using Shared.Models.Base;
using Shared.Models.Online.iRemux;
using Shared.Models.Templates;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public struct iRemuxInvoke
    {
        #region iRemuxInvoke
        string host;
        string apihost;
        Func<string, ValueTask<string>> onget;
        Func<string, string, ValueTask<string>> onpost;
        Func<string, string> onstreamfile;
        Func<string, string> onlog;
        Action requesterror;

        public iRemuxInvoke(string host, string apihost, Func<string, ValueTask<string>> onget, Func<string, string, ValueTask<string>> onpost, Func<string, string> onstreamfile, Func<string, string> onlog = null, Action requesterror = null)
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
        async public ValueTask<EmbedModel> Embed(string title, string original_title, int year, string link)
        {
            var result = new EmbedModel();

            if (string.IsNullOrEmpty(link))
            {
                string search = await onget($"{apihost}/index.php?do=search&subaction=search&from_page=0&story={HttpUtility.UrlEncode(title ?? original_title)}");
                if (search == null)
                {
                    requesterror?.Invoke();
                    return null;
                }

                string stitle = title?.ToLower();
                string sorigtitle = original_title?.ToLower();

                foreach (string row in search.Split("item--announce").Skip(1))
                {
                    var g = Regex.Match(row, "class=\"item__title( [^\"]+)?\"><a href=\"(?<link>https?://[^\"]+)\">(?<name>[^<]+)</a>").Groups;

                    string name = g["name"].Value.ToLower();
                    if (name.Contains("сезон") || name.Contains("серии") || name.Contains("серия"))
                        continue;

                    if ((!string.IsNullOrEmpty(stitle) && name.Contains(stitle)) || (!string.IsNullOrEmpty(sorigtitle) && name.Contains(sorigtitle)))
                    {
                        if (string.IsNullOrEmpty(g["link"].Value))
                            continue;

                        if (name.Contains($"({year}/"))
                        {
                            result.similars.Add(new Similar()
                            {
                                title = name,
                                year = year.ToString(),
                                href = g["link"].Value
                            });
                        }
                    }
                }

                if (result.similars.Count == 0)
                {
                    if (search.Contains(">Поиск по сайту<"))
                        return new EmbedModel() { IsEmpty = true };

                    return null;
                }

                if (result.similars.Count > 1)
                    return result;

                link = result.similars[0].href;
            }

            string news = await onget(link);
            if (news == null)
            {
                requesterror?.Invoke();
                return null;
            }

            string content = news.Split("page__desc")[1].Split("page__dl")[0];
            if (!content.Contains("cloud.mail.ru/public/"))
                return null;

            result.content = content.Replace("<!--colorend--></span><!--/colorend-->", "");
            return result;
        }
        #endregion

        #region Html
        public string Html(EmbedModel result, string title, string original_title, int year, bool rjson = false)
        {
            if (result == null || result.IsEmpty)
                return string.Empty;

            string enc_title = HttpUtility.UrlEncode(title);
            string enc_original_title = HttpUtility.UrlEncode(original_title);

            #region similar
            if (result.content == null)
            {
                if (result.similars != null && result.similars.Count > 0)
                {
                    var stpl = new SimilarTpl(result.similars.Count);

                    foreach (var similar in result.similars)
                    {
                        string link = host + $"lite/remux?title={enc_title}&original_title={enc_original_title}&year={year}&href={HttpUtility.UrlEncode(similar.href)}";

                        stpl.Append(similar.title, similar.year, string.Empty, link);
                    }

                    return rjson ? stpl.ToJson() : stpl.ToHtml();
                }

                return string.Empty;
            }
            #endregion

            var mtpl = new MovieTpl(title, original_title, 4);

            foreach (Match m in Regex.Matches(result.content, $">([^<]+)(<[^>]+>)?<a href=\"https?://cloud.mail.ru/public/([^\"]+)\""))
            {
                string linkid = m.Groups[3].Value;
                if (string.IsNullOrEmpty(linkid))
                    continue;

                foreach (string q in new string[] { "2160p", "1080p", "720p", "480p" })
                {
                    string _qs = q == "480p" ? "1400" : q;
                    if (m.Groups[1].Value.Contains(_qs))
                    {
                        mtpl.Append(q, host + $"lite/remux/movie?linkid={linkid}&quality={q}&title={enc_title}&original_title={enc_original_title}", "call");
                        break;
                    }
                }
            }

            return rjson ? mtpl.ToJson(reverse: true) : mtpl.ToHtml(reverse: true);
        }
        #endregion


        #region Weblink
        async public ValueTask<string> Weblink(string linkid)
        {
            string html = await onget($"https://cloud.mail.ru/public/{linkid}");
            if (html == null)
            {
                requesterror?.Invoke();
                return null;
            }

            string weblinkRow = StringConvert.FindLastText(html, "\"weblink_get\"", "}");
            if (weblinkRow == null)
                return null;

            string location = Regex.Match(weblinkRow, "\"url\": ?\"(https?://[^/]+)").Groups[1].Value;
            if (string.IsNullOrEmpty(location))
                return null;

            return $"{location}/weblink/view/{linkid}";
        }
        #endregion

        #region Movie
        public string Movie(in string weblink, in string quality, in string title, in string original_title, VastConf vast = null)
        {
            return VideoTpl.ToJson("play", onstreamfile?.Invoke(weblink), (title ?? original_title), quality: quality, vast: vast);
        }
        #endregion
    }
}
