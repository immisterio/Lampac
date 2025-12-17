using HtmlAgilityPack;
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

            var doc = new HtmlDocument();
            doc.LoadHtml(news);

            var pageDescNode = doc.DocumentNode.SelectSingleNode("//div[@class='page__desc']");

            if (pageDescNode == null || !pageDescNode.InnerHtml.Contains("cloud.mail.ru/public/"))
                return null;

            result.content = pageDescNode.InnerHtml;
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

            foreach (var node in HtmlParse.Nodes(result.content, "//div[@class='quote']"))
            {
                string linkid = node.Regex("href=\"https?://cloud.mail.ru/public/([^\"]+)\"");
                if (string.IsNullOrEmpty(linkid))
                    continue;

                bool setAuto = true;

                foreach (string q in new string[] { "2160p", "1080p", "720p", "480p" })
                {
                    string _qs = q == "480p" ? "1400" : q;
                    if (node.row.InnerHtml.Contains(_qs))
                    {
                        mtpl.Append(q, host + $"lite/remux/movie?linkid={linkid}&quality={q}&title={enc_title}&original_title={enc_original_title}", "call");
                        setAuto = false;
                        break;
                    }
                }

                if (setAuto)
                    mtpl.Append("480p", host + $"lite/remux/movie?linkid={linkid}&quality=480p&title={enc_title}&original_title={enc_original_title}", "call");
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
