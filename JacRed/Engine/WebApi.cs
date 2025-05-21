using Jackett;
using JacRed.Models;
using JacRed.Models.Tracks;
using Lampac.Engine.CORE;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace JacRed.Engine
{
    public static class WebApi
    {
        #region Indexers
        async public static Task<List<TorrentDetails>> Indexers(string query, string title, string title_original, int year, int is_serial, Dictionary<string, string> category)
        {
            var queryString = new StringBuilder();

            if (!string.IsNullOrEmpty(title))
                queryString.Append($"&title={HttpUtility.UrlEncode(title)}");

            if (!string.IsNullOrEmpty(title_original))
                queryString.Append($"&title_original={HttpUtility.UrlEncode(title_original)}");

            if (year > 0)
                queryString.Append($"&year={year}");

            if (is_serial > 0)
                queryString.Append($"&is_serial={is_serial}");

            if (category != null && category.Count > 0)
                queryString.Append($"&category[]={category.First().Value}");

            var root = await HttpClient.Get<JObject>($"{ModInit.conf.webApiHost}/api/v2.0/indexers/all/results?query={HttpUtility.UrlEncode(query)}" + queryString.ToString(), timeoutSeconds: 8);
            if (root == null)
                return new List<TorrentDetails>();

            var results = root.GetValue("Results")?.ToObject<JArray>();
            if (results == null || results.Count == 0)
                return new List<TorrentDetails>();

            var torrents = new List<TorrentDetails>(results.Count);

            foreach (var torrent in results)
            {
                try
                {
                    torrents.Add(new TorrentDetails()
                    {
                        trackerName = torrent.Value<string>("Tracker"),
                        url = torrent.Value<string>("Details"),
                        title = torrent.Value<string>("Title"),
                        sid = torrent.Value<int>("Seeders"),
                        pir = torrent.Value<int>("Peers"),
                        size = torrent.Value<double>("Size"),
                        magnet = torrent.Value<string>("MagnetUri"),
                        createTime = torrent.Value<DateTime>("PublishDate"),
                        ffprobe = torrent["ffprobe"]?.ToObject<List<ffStream>>()
                    });
                }
                catch { }
            }

            return torrents;
        }
        #endregion

        #region Api
        public static Task<List<TorrentDetails>> Api(string search)
        {
            return Indexers(search, null, null, 0, 0, null);
        }
        #endregion
    }
}
