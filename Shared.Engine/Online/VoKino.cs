using Shared.Model.Online.VoKino;
using System.Text;
using System.Text.Json;

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

        public VoKinoInvoke(string? host, string apihost, string token, Func<string, ValueTask<string?>> onget, Func<string, string> onstreamfile, Func<string, string>? onlog = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.token = token;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
        }
        #endregion

        #region Embed
        public async ValueTask<List<Сhannel>?> Embed(long kinopoisk_id)
        {
            try
            {
                string? json = await onget($"{apihost}/v2/list?name=%2B{kinopoisk_id}&token={token}");
                if (json == null)
                    return null;

                var root = JsonSerializer.Deserialize<RootObject>(json);
                if (root?.channels == null || root.channels.Count == 0)
                    return null;

                string? id = root.channels.First().details?.id;
                if (string.IsNullOrWhiteSpace(id))
                    return null;

                json = await onget($"{apihost}/v2/online/vokino?id={id}&token={token}");
                if (json == null)
                    return null;

                root = JsonSerializer.Deserialize<RootObject>(json);
                if (root?.channels == null || root.channels.Count == 0)
                    return null;

                return root.channels;
            }
            catch { }

            return null;
        }
        #endregion

        #region Html
        public string Html(List<Сhannel> channels, string? title, string? original_title)
        {
            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            foreach (var ch in channels)
            {
                string link = onstreamfile(ch.stream_url);
                string name = ch.quality_full;
                if (!string.IsNullOrWhiteSpace(name.Replace("2160p.", "")))
                {
                    name = name.Replace("2160p.", "4K ");

                    if (ch.extra != null && ch.extra.TryGetValue("size", out string? size) && !string.IsNullOrEmpty(size))
                        name += $" - {size}";
                }

                html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + link + "\",\"title\":\"" + (title ?? original_title) + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + name + "</div></div>");
                firstjson = false;
            }

            return html.ToString() + "</div>";
        }
        #endregion
    }
}
