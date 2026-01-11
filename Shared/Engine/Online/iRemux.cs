using Shared.Engine.RxEnumerate;
using Shared.Models;
using Shared.Models.Base;
using Shared.Models.Online.iRemux;
using Shared.Models.Templates;
using System.Web;

namespace Shared.Engine.Online
{
    public struct iRemuxInvoke
    {
        #region iRemuxInvoke
        string host, cookie, apihost;
        HttpHydra httpHydra;
        Func<string, string> onstreamfile;
        Action requesterror;

        public iRemuxInvoke(string host, string apihost, string cookie, HttpHydra httpHydra, Func<string, string> onstreamfile, Action requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.cookie = cookie;
            this.httpHydra = httpHydra;
            this.onstreamfile = onstreamfile;
            this.requesterror = requesterror;
        }
        #endregion

        #region Embed
        async public Task<EmbedModel> Embed(string title, string original_title, int year, string link)
        {
            var result = new EmbedModel();

            if (string.IsNullOrEmpty(link))
            {
                bool reqOk = false;

                string uri = $"{apihost}/index.php?do=search&subaction=search&from_page=0&story={HttpUtility.UrlEncode(title ?? original_title)}";

                await httpHydra.GetSpan(uri, addheaders: HeadersModel.Init("cookie", cookie), spanAction: search => 
                {
                    reqOk = search.Contains(">Поиск по сайту<", StringComparison.OrdinalIgnoreCase);

                    string stitle = StringConvert.SearchName(title);
                    string sorigtitle = StringConvert.SearchName(original_title);

                    var rx = Rx.Split("item--announce", search, 1);

                    foreach (var row in rx.Rows())
                    {
                        var g = row.Groups("class=\"item__title( [^\"]+)?\"><a href=\"(?<link>https?://[^\"]+)\">(?<name>[^<]+)</a>");

                        string name = g["name"].Value.ToLower();
                        if (string.IsNullOrWhiteSpace(name) || name.Contains("сезон") || name.Contains("серии") || name.Contains("серия"))
                            continue;

                        if (string.IsNullOrEmpty(g["link"].Value))
                            continue;

                        bool find = false;
                        string _sname = StringConvert.SearchName(name);

                        if (!string.IsNullOrEmpty(stitle))
                            find = _sname.Contains(stitle);

                        if (!find && !string.IsNullOrEmpty(sorigtitle))
                            find = _sname.Contains(sorigtitle);

                        if (find && name.Contains($"({year}/"))
                        {
                            result.similars.Add(new Similar()
                            {
                                title = name,
                                year = year.ToString(),
                                href = g["link"].Value
                            });
                        }
                    }
                });

                if (result.similars.Count == 0)
                {
                    if (reqOk)
                        return new EmbedModel() { IsEmpty = true };

                    requesterror?.Invoke();
                    return null;
                }

                if (result.similars.Count > 1)
                    return result;

                link = result.similars[0].href;
            }


            await httpHydra.GetSpan(link, addheaders: HeadersModel.Init("cookie", cookie), spanAction: news =>
            {
                result.content = HtmlSpan.Node(news, "div", "class", "page__desc", HtmlSpanTargetType.Exact).ToString();
            });

            if (string.IsNullOrEmpty(result.content) || !result.content.Contains("cloud.mail.ru/public/"))
            {
                requesterror?.Invoke();
                return null;
            }

            return result;
        }
        #endregion

        #region Tpl
        public ITplResult Tpl(EmbedModel result, string title, string original_title, int year)
        {
            if (result == null || result.IsEmpty)
                return default;

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

                    return stpl;
                }

                return default;
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

            mtpl.Reverse();

            return mtpl;
        }
        #endregion


        #region Weblink
        async public Task<string> Weblink(string linkid)
        {
            string location = null;

            await httpHydra.GetSpan($"https://cloud.mail.ru/public/{linkid}", html => 
            {
                var rx = Rx.Split("\"weblink_get\":", html, 1);
                if (rx.Count > 0)
                    location = rx[0].Match("\"url\": ?\"(https?://[^/]+)");
            });

            if (string.IsNullOrEmpty(location))
            {
                requesterror?.Invoke();
                return null;
            }

            return $"{location}/weblink/view/{linkid}";
        }
        #endregion

        #region Movie
        public string Movie(string weblink, string quality, string title, string original_title, VastConf vast = null)
        {
            return VideoTpl.ToJson("play", onstreamfile?.Invoke(weblink), (title ?? original_title), quality: quality, vast: vast);
        }
        #endregion
    }
}
