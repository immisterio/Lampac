using Shared.Model.Online.VoKino;
using Shared.Model.Templates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class VoKinoInvoke
    {
        #region VoKinoInvoke
        string? host;
        string apihost;
        string token;
        Func<string, ValueTask<string?>> onget;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;
        Action? requesterror;

        public VoKinoInvoke(string? host, string apihost, string token, Func<string, ValueTask<string?>> onget, Func<string, string> onstreamfile, Func<string, string>? onlog = null, Action? requesterror = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.token = token;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.requesterror = requesterror;
        }
        #endregion

        #region Embed
        public async ValueTask<List<Сhannel>?> Embed(long kinopoisk_id)
        {
            try
            {
                string? json = await onget($"{apihost}/v2/online/vokino/{kinopoisk_id}?token={token}");
                if (json == null)
                {
                    requesterror?.Invoke();
                    return null;
                }

                if (json.StartsWith("{\"error\":"))
                    return null;

                var root = JsonSerializer.Deserialize<RootObject>(json);
                if (root?.channels == null || root.channels.Count == 0)
                    return null;

                return root.channels;
            }
            catch { }

            return null;
        }
        #endregion

        #region Html
        public string Html(List<Сhannel>? channels, long kinopoisk_id, string? title, string? original_title, int s)
        {
            if (channels == null || channels.Count == 0)
                return string.Empty;

            if (channels.First().playlist_url == "submenu")
            {
                if (s == -1)
                {
                    var tpl = new SeasonTpl(quality: channels[0].quality_full?.Replace("2160p.", "4K "));

                    foreach (var ch in channels)
                    {
                        string sname = Regex.Match(ch.title, "^([0-9]+)").Groups[1].Value;
                        tpl.Append(ch.title, host + $"lite/vokino?kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={sname}");
                    }

                    return tpl.ToHtml();
                }
                else
                {
                    var tpl = new EpisodeTpl();

                    foreach (var e in channels.First(i => i.title.StartsWith($"{s} ")).submenu)
                    {
                        string ename = Regex.Match(e.title, "^([0-9]+)").Groups[1].Value;
                        tpl.Append(e.title, $"{title ?? original_title} ({e.title})", s.ToString(), ename, onstreamfile(e.stream_url));
                    }

                    return tpl.ToHtml();
                }
            }
            else
            {
                var mtpl = new MovieTpl(title, original_title, channels.Count);

                foreach (var ch in channels)
                {
                    string name = ch.quality_full;
                    if (!string.IsNullOrWhiteSpace(name.Replace("2160p.", "")))
                    {
                        name = name.Replace("2160p.", "4K ");

                        if (ch.extra != null && ch.extra.TryGetValue("size", out string? size) && !string.IsNullOrEmpty(size))
                            name += $" - {size}";
                    }

                    mtpl.Append(name, onstreamfile(ch.stream_url));
                }

                return mtpl.ToHtml();
            }
        }
        #endregion
    }
}
