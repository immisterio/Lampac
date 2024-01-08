using Shared.Model.Online.VoKino;
using Shared.Model.Templates;
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
                string? json = await onget($"{apihost}/v2/online/vokino/{kinopoisk_id}?token={token}");
                if (json == null)
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
        public string Html(List<Сhannel>? channels, string? title, string? original_title)
        {
            if (channels == null || channels.Count == 0)
                return string.Empty;

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
        #endregion
    }
}
