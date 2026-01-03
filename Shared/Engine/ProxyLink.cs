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
using System.Threading;

namespace Shared.Engine
{
    public class ProxyLink : IProxyLink
    {
        #region ProxyLink
        static readonly ConcurrentDictionary<string, ProxyLinkModel> links = new();

        static readonly Timer _cronTimer = new Timer(Cron, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        public static int Stat_ContLinks => links.IsEmpty ? 0 : links.Count;
        #endregion


        #region Encrypt
        public string Encrypt(string uri, string plugin, DateTime ex = default, bool IsProxyImg = false) => Encrypt(uri, null, verifyip: false, ex: ex, plugin: plugin, IsProxyImg: IsProxyImg);

        public static string Encrypt(string uri, ProxyLinkModel p, bool forceMd5 = false) => Encrypt(uri, p.reqip, p.headers, p.proxy, p.plugin, p.verifyip, forceMd5: forceMd5);

        public static string Encrypt(string uri, string reqip, List<HeadersModel> headers = null, WebProxy proxy = null, string plugin = null, bool verifyip = true, DateTime ex = default, bool forceMd5 = false, bool IsProxyImg = false)
        {
            if (string.IsNullOrWhiteSpace(uri))
                return string.Empty;

            string hash;
            bool IsMd5 = false;
            string uri_clear = uri.Contains("#") ? uri.Split("#")[0].Trim() : uri.Trim();

            if (plugin == "posterapi")
            {
                hash = AesTo.Encrypt(JsonSerializer.Serialize(new { u = uri_clear }));
            }
            else if (!forceMd5 && AppInit.conf.serverproxy.encrypt_aes && (headers == null || headers.Count == 0) && proxy == null && !uri_clear.Contains(" or "))
            {
                if (verifyip && AppInit.conf.serverproxy.verifyip)
                {
                    hash = AesTo.Encrypt(JsonSerializer.Serialize(new
                    {
                        p = plugin,
                        u = uri_clear,
                        i = reqip,
                        v = true,
                        e = DateTime.Now.AddHours(36)
                    }));
                }
                else
                {
                    hash = AesTo.Encrypt(JsonSerializer.Serialize(new { p = plugin, u = uri_clear }));
                }
            }
            else
            {
                IsMd5 = true;
                hash = CrypTo.md5(uri_clear + (verifyip && AppInit.conf.serverproxy.verifyip ? reqip : string.Empty));
            }

            if (IsProxyImg)
            {
                if (uri.Contains(".png"))
                    hash += ".png";
                else if (uri.Contains(".webp"))
                    hash += ".webp";
                else
                    hash += ".jpg";
            }
            else
            {
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
                else if (uri.Contains(".mov"))
                    hash += ".mov";
                else if (uri.Contains(".mkv"))
                    hash += ".mkv";
                else if (uri.Contains(".aac"))
                    hash += ".aac";
                else if (uri.Contains(".vtt"))
                    hash += ".vtt";
                else if (uri.Contains(".srt"))
                    hash += ".srt";
                else if (uri.Contains(".jpg") || uri.Contains(".jpeg"))
                    hash += ".jpg";
                else if (uri.Contains(".png"))
                    hash += ".png";
                else if (uri.Contains(".webp"))
                    hash += ".webp";
            }

            if (IsMd5)
            {
                var md = new ProxyLinkModel(verifyip ? reqip : null, headers, proxy, uri_clear, plugin, verifyip, ex: ex);
                links.AddOrUpdate(hash, md, (d, u) => md);
            }

            return hash;
        }
        #endregion

        #region Decrypt
        public static ProxyLinkModel Decrypt(string hash, string reqip)
        {
            if (string.IsNullOrEmpty(hash))
                return null;

            if (IsAes(hash))
            {
                hash = Regex.Replace(hash, "\\.[a-z0-9]+$", "", RegexOptions.IgnoreCase);

                string dec = AesTo.Decrypt(hash);
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

                return new ProxyLinkModel(reqip, headers, null, root["u"].GetValue<string>(), root["p"]?.GetValue<string>());
            }

            if (!links.TryGetValue(hash, out ProxyLinkModel val))
            {
                try
                {
                    if (IsUseSql(hash))
                    {
                        using (var sqlDb = ProxyLinkContext.Factory != null
                            ? ProxyLinkContext.Factory.CreateDbContext()
                            : new ProxyLinkContext())
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
        #endregion

        #region IsAes
        public static bool IsAes(string hash)
        {
            if (hash.StartsWith("http"))
                return false;

            if (hash.Split('?', '&', '.')[0].Length == 32)
                return false;

            return true;
        }
        #endregion

        #region IsUseSql
        static bool IsUseSql(string hash)
        {
            if (AppInit.conf.mikrotik)
                return false;

            bool useSql = true;
            if (AppInit.conf.serverproxy.image.noSqlDb)
            {
                string extension = Regex.Match(hash, "\\.([a-z0-9]+)$", RegexOptions.IgnoreCase).Groups[1].Value;

                if (extension is "jpg" or "jpeg" or "png" or "webp")
                    useSql = false;
            }

            return useSql;
        }
        #endregion


        #region Cron
        static HashSet<string> tempLinks = new(1000);

        static int cronRound = 0;

        static DateTime _nextClearDb = DateTime.Now.AddMinutes(5);

        static int _updatingDb = 0;

        async static void Cron(object state)
        {
            if (links.IsEmpty)
                return;

            if (Interlocked.Exchange(ref _updatingDb, 1) == 1)
                return;

            try
            {
                if (cronRound >= 60)
                {
                    cronRound = 0;
                    tempLinks.Clear();
                }

                cronRound++;
                var now = DateTime.Now;

                if (now > _nextClearDb)
                {
                    _nextClearDb = now.AddMinutes(5);

                    using (var sqlDb = new ProxyLinkContext())
                    {
                        await sqlDb.links
                            .Where(i => now > i.ex)
                            .ExecuteDeleteAsync();
                    }
                }
                else
                {
                    var sqlLinks = new HashSet<string>(Math.Min(100, links.Count));
                    var delete_ids = new HashSet<string>(Math.Min(100, links.Count));

                    foreach (var link in links)
                    {
                        try
                        {
                            if (IsUseSql(link.Key) == false || link.Value.proxy != null || now.AddMinutes(5) > link.Value.ex || link.Value.uri.Contains(" or "))
                            {
                                if (now > link.Value.ex)
                                    delete_ids.Add(link.Key);
                            }
                            else
                            {
                                if (tempLinks.Contains(link.Key))
                                    delete_ids.Add(link.Key);
                                else
                                {
                                    sqlLinks.Add(link.Key);
                                }
                            }
                        }
                        catch { }
                    }

                    if (delete_ids.Count > 0)
                    {
                        foreach (string removeId in delete_ids)
                            links.TryRemove(removeId, out _);
                    }

                    if (sqlLinks.Count > 0)
                    {
                        using (var sqlDb = new ProxyLinkContext())
                        {
                            await sqlDb.links
                                .Where(x => sqlLinks.Contains(x.Id))
                                .ExecuteDeleteAsync();

                            foreach (string linkId in sqlLinks)
                            {
                                if (links.TryGetValue(linkId, out var link))
                                {
                                    if (link.id == null)
                                        link.id = linkId;

                                    sqlDb.links.Add(new ProxyLinkSqlModel()
                                    {
                                        Id = linkId,
                                        ex = link.ex,
                                        json = JsonSerializer.Serialize(link)
                                    });
                                }
                            }

                            await sqlDb.SaveChangesAsync();

                            foreach (string removeLink in sqlLinks)
                            {
                                tempLinks.Add(removeLink);
                                links.TryRemove(removeLink, out _);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) 
            { 
                Console.WriteLine($"ProxyLink: {ex}"); 
            }
            finally 
            {
                Volatile.Write(ref _updatingDb, 0);
            }
        }
        #endregion
    }
}
