using Shared.Models.Base;
using Shared.Models.Templates;
using Shared.Services;
using Shared.Services.HTML;
using Shared.Services.RxEnumerate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace BamBoo;

public struct BamBooInvoke
{
    string host;
    string apihost;
    HttpHydra http;
    Func<string, string> onstreamfile;

    public BamBooInvoke(string host, string apihost, HttpHydra httpHydra, Func<string, string> onstreamfile)
    {
        this.host = host != null ? $"{host}/" : null;
        this.apihost = apihost;
        http = httpHydra;
        this.onstreamfile = onstreamfile;
    }

    #region Search
    public async Task<EmbedModel> Search(string story)
    {
        var result = new EmbedModel();
        string _apihost = apihost;
        bool empty = false;

        await http.GetSpan($"{apihost}/index.php?do=search&subaction=search&story={HttpUtility.UrlEncode(story)}", html =>
        {
            var similars = new List<Similar>();
            empty = html.Contains("main-title-page", StringComparison.Ordinal);

            foreach (ReadOnlySpan<char> row in HtmlSpan.Nodes(html, "li", "class", "col-sm-6 col-md-4 col-lg-3 col-xl-3 slide-item mt-5", HtmlSpanTargetType.Exact))
            {
                string link = Rx.Match(row, "href=\"https?://[^/]+/([^\"]+\\.html)\"");
                string name = Rx.Match(row, "<h6><a [^>]+>([^<]+)</a></h6>");

                if (string.IsNullOrEmpty(link) || string.IsNullOrEmpty(name))
                    continue;

                string img = Rx.Match(row, "src=\"/(uploads/[^\"]+)\"");
                if (img != null)
                    img = $"{_apihost}/{img}";

                var model = new Similar()
                {
                    title = name,
                    year = Rx.Match(row, "/year/([0-9]+)/\"") ?? string.Empty,
                    href = link,
                    img = img
                };

                similars.Add(model);
            }

            if (similars.Count > 0)
                result.similars = similars;
        });

        if (result.similars == null)
            return empty ? new EmbedModel() { IsEmpty = true } : null;

        return result;
    }
    #endregion

    #region Embed
    public async Task<EmbedModel> Embed(string link)
    {
        var result = new EmbedModel();

        string pageHtml = await http.Get($"{apihost}/{link}");
        if (pageHtml == null)
            return null;

        var playNodes = HtmlParse.Nodes(pageHtml, "//span[contains(concat(' ', normalize-space(@class), ' '), ' play_me ')]");
        if (playNodes == null || playNodes.Count == 0)
            return null;

        string title = Rx.Match(pageHtml, "<title>([^<]+)</title>");
        int.TryParse(Regex.Match(title, @"([0-9]{1,2})\s*Сезон", RegexOptions.IgnoreCase).Groups[1].Value, out int season);

        result.season = season > 0 ? season : 1;

        var movie = new List<Video>();
        var serial = new Dictionary<string, List<Series>>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in playNodes)
        {
            string file = node.SelectText(string.Empty, "data-file");
            if (string.IsNullOrEmpty(file) || !file.Contains("m3u8", StringComparison.OrdinalIgnoreCase))
                continue;

            string eptitle = node.SelectText(string.Empty, "data-title");
            if (string.IsNullOrEmpty(eptitle))
                eptitle = node.SelectText(".//p");

            string voice = node.SelectText("preceding::h3[1]");
            if (string.IsNullOrEmpty(voice))
                voice = "По умолчанию";

            string type = node.SelectText(string.Empty, "data-type");

            if (Regex.IsMatch(eptitle ?? string.Empty, "Сер[іи]я|Episode", RegexOptions.IgnoreCase) || playNodes.Count > 1)
            {
                result.isSerial = true;

                if (!serial.TryGetValue(voice, out var episodes))
                {
                    episodes = new List<Series>(20);
                    serial[voice] = episodes;
                }

                episodes.Add(new Series
                {
                    title = string.IsNullOrEmpty(eptitle) ? $"Серія {episodes.Count + 1}" : eptitle,
                    file = file,
                    type = type
                });
            }
            else
            {
                movie.Add(new Video
                {
                    title = voice,
                    file = file,
                    type = type
                });
            }
        }

        if (result.isSerial)
        {
            result.serial = serial
                .Select(i => new Voice
                {
                    title = i.Key,
                    folder = i.Value
                })
                .ToList();
        }
        else if (movie.Count > 0)
        {
            result.movie = movie;
        }

        if ((result.serial == null || result.serial.Count == 0) && (result.movie == null || result.movie.Count == 0))
            return null;

        return result;
    }
    #endregion

    #region Tpl
    public ITplResult Tpl(EmbedModel result, string title, string original_title, int year, int t, string href, VastConf vast = null, bool rjson = false)
    {
        if (result == null || result.IsEmpty)
            return default;

        if (!result.isSerial)
        {
            var mtpl = new MovieTpl(title, original_title, 1);

            foreach (var item in result.movie)
                mtpl.Append(item.title, onstreamfile.Invoke(item.file), vast: vast);

            return mtpl;
        }
        else
        {
            string enc_href = HttpUtility.UrlEncode(href);
            string enc_title = HttpUtility.UrlEncode(title);
            string enc_original_title = HttpUtility.UrlEncode(original_title);

            try
            {
                var vtpl = new VoiceTpl();

                for (int i = 0; i < result.serial.Count; i++)
                {
                    if (t == -1)
                        t = i;

                    vtpl.Append(
                        result.serial[i].title,
                        t == i,
                        host + $"lite/bamboo?rjson={rjson}&title={enc_title}&original_title={enc_original_title}&year={year}&href={enc_href}&t={i}"
                    );
                }

                string sArch = result.season.ToString();
                var episodes = result.serial[t].folder;
                var etpl = new EpisodeTpl(vtpl, episodes.Count);

                foreach (var episode in episodes)
                {
                    etpl.Append(
                        episode.title,
                        title ?? original_title,
                        sArch,
                        Regex.Match(episode.title ?? string.Empty, "([0-9]+)").Groups[1].Value,
                        onstreamfile.Invoke(episode.file),
                        vast: vast
                    );
                }

                return etpl;
            }
            catch
            {
                return default;
            }
        }
    }
    #endregion
}
