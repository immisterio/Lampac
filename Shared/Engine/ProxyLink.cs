using Microsoft.EntityFrameworkCore;
using Shared.Models;
using Shared.Models.Base;
using Shared.Models.Proxy;
using Shared.Models.SQL;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Shared.Engine
{
    public class ProxyLink : IProxyLink
    {
        public string Encrypt(string uri, string plugin, DateTime ex = default) => Encrypt(uri, null, verifyip: false, ex: ex, plugin: plugin);


        static ConcurrentDictionary<string, ProxyLinkModel> links = new ConcurrentDictionary<string, ProxyLinkModel>();

        public static string Encrypt(string uri, ProxyLinkModel p, bool forceMd5 = false) => Encrypt(uri, p.reqip, p.headers, p.proxy, p.plugin, p.verifyip, forceMd5: forceMd5);

        public static string Encrypt(string uri, string reqip, List<HeadersModel> headers = null, WebProxy proxy = null, string plugin = null, bool verifyip = true, DateTime ex = default, bool forceMd5 = false)
        {
            if (string.IsNullOrWhiteSpace(uri))
                return string.Empty;

            string hash;
            bool IsMd5 = false;
            string uri_clear = uri.Contains("#") ? uri.Split("#")[0].Trim() : uri.Trim();

            if (!forceMd5 && AppInit.conf.serverproxy.encrypt_aes && (headers == null || headers.Count == 0) && proxy == null)
            {
                if (verifyip && AppInit.conf.serverproxy.verifyip)
                {
                    hash = "aes:" + AesTo.Encrypt(JsonSerializer.Serialize(new
                    {
                        u = uri_clear,
                        i = reqip,
                        v = true,
                        e = DateTime.Now.AddHours(36)
                    }));
                }
                else
                {
                    hash = "aes:" + AesTo.Encrypt(JsonSerializer.Serialize(new { u = uri_clear }));
                }
            }
            else
            {
                IsMd5 = true;
                hash = CrypTo.md5(uri_clear + (verifyip && AppInit.conf.serverproxy.verifyip ? reqip : string.Empty));
            }

            if (uri.Contains(".m3u8"))
                hash += ".m3u8";
            else if (uri.Contains(".m3u"))
                hash += ".m3u";
            else if (uri.Contains(".mpd"))
                hash += ".mpd";
            else if (uri.Contains(".webm"))
                hash += ".webm";
            else if (uri.Contains(".ts"))
                hash += ".ts";
            else if (uri.Contains(".m4s"))
                hash += ".m4s";
            else if (uri.Contains(".mp4"))
                hash += ".mp4";
            else if (uri.Contains(".mkv"))
                hash += ".mkv";
            else if (uri.Contains(".aac"))
                hash += ".aac";
            else if (uri.Contains(".jpg") || uri.Contains(".jpeg"))
                hash += ".jpg";
            else if (uri.Contains(".png"))
                hash += ".png";
            else if (uri.Contains(".webp"))
                hash += ".webp";
            else if (uri.Contains(".vtt"))
                hash += ".vtt";
            else if (uri.Contains(".srt"))
                hash += ".srt";

            if (IsMd5)
            {
                var md = new ProxyLinkModel(verifyip ? reqip : null, headers, proxy, uri_clear, plugin, verifyip, ex: ex);
                links.AddOrUpdate(hash, md, (d, u) => md);
            }

            return hash;
        }

        public static ProxyLinkModel Decrypt(string hash, string reqip)
        {
            if (string.IsNullOrEmpty(hash))
                return null;

            if (hash.StartsWith("aes:"))
            {
                hash = Regex.Replace(hash, "\\.[a-z0-9]+$", "", RegexOptions.IgnoreCase);

                string dec = AesTo.Decrypt(hash.Replace("aes:", ""));
                if (string.IsNullOrEmpty(dec))
                    return null;

                var root = JsonNode.Parse(dec);

                if (root["v"]?.GetValue<bool>() == true)
                {
                    if (reqip != null && root["i"].GetValue<string>() != reqip)
                        return null;

                    if (DateTime.Now > root["e"].GetValue<DateTime>())
                        return null;
                }

                var headers = HeadersModel.Init(root["h"]?.Deserialize<Dictionary<string, string>>());
                return new ProxyLinkModel(reqip, headers, null, root["u"].GetValue<string>());
            }

            if (!links.TryGetValue(hash, out ProxyLinkModel val))
            {
                try
                {
                    if (!AppInit.conf.mikrotik)
                    {
                        using (var sqlDb = new ProxyLinkContext())
                        {
                            var link = sqlDb.links.Find(hash);
                            if (link != null && link.ex > DateTime.Now)
                            {
                                val = JsonSerializer.Deserialize<ProxyLinkModel>(link.json);
                                val.id = link.Id;
                                val.ex = link.ex;
                            }
                        }
                    }
                }
                catch { }
            }

            if (val != null)
            {
                if (val.verifyip == false || AppInit.conf.serverproxy.verifyip == false || val.reqip == string.Empty || reqip == null || reqip == val.reqip)
                    return val;
            }

            return null;
        }


        static DateTime _nextClearDb = DateTime.Now.AddMinutes(5);

        async public static Task Cron()
        {
            int round = 0;
            var hash = new HashSet<string>();

            while (true)
            {
                try
                {
                    if (round == 60)
                    {
                        round = 0;
                        hash.Clear();
                    }

                    round++;
                    await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);

                    using (var sqlDb = new ProxyLinkContext())
                    {
                        if (DateTime.Now > _nextClearDb)
                        {
                            var now = DateTime.Now;

                            sqlDb.links
                                 .AsNoTracking()
                                 .Where(i => now > i.ex)
                                 .ExecuteDelete();

                            _nextClearDb = DateTime.Now.AddHours(1);
                            continue;
                        }

                        foreach (var link in links)
                        {
                            try
                            {
                                if (AppInit.conf.mikrotik || link.Value.proxy != null || DateTime.Now.AddMinutes(5) > link.Value.ex)
                                {
                                    if (DateTime.Now > link.Value.ex)
                                        links.TryRemove(link.Key, out _);
                                }
                                else
                                {
                                    if (hash.Contains(link.Key))
                                        links.TryRemove(link.Key, out _);
                                    else
                                    {
                                        link.Value.id = link.Key;

                                        var doc = sqlDb.links.Find(link.Key);
                                        if (doc != null)
                                        {
                                            doc.ex = link.Value.ex;
                                            doc.json = JsonSerializer.Serialize(link.Value);
                                        }
                                        else
                                        {
                                            sqlDb.links.Add(new ProxyLinkSqlModel() 
                                            {
                                                Id = link.Key,
                                                ex = link.Value.ex,
                                                json = JsonSerializer.Serialize(link.Value)
                                            });
                                        }

                                        if (sqlDb.SaveChanges() > 0)
                                        {
                                            hash.Add(link.Key);
                                            links.TryRemove(link.Key, out _);
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
        }
    }
}
