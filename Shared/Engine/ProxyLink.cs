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
        static ConcurrentDictionary<string, ProxyLinkModel> links = new ConcurrentDictionary<string, ProxyLinkModel>();

        static Timer _cronTimer;

        static ProxyLink()
        {
            _cronTimer = new Timer(Cron, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }
        #endregion


        #region Encrypt
        public string Encrypt(string uri, string plugin, DateTime ex = default) => Encrypt(uri, null, verifyip: false, ex: ex, plugin: plugin);

        public static string Encrypt(string uri, ProxyLinkModel p, bool forceMd5 = false) => Encrypt(uri, p.reqip, p.headers, p.proxy, p.plugin, p.verifyip, forceMd5: forceMd5);

        public static string Encrypt(string uri, string reqip, List<HeadersModel> headers = null, WebProxy proxy = null, string plugin = null, bool verifyip = true, DateTime ex = default, bool forceMd5 = false)
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


        #region Cron
        static HashSet<string> tempLinks = new();

        static int cronRound = 0;

        static DateTime _nextClearDb = DateTime.Now.AddHours(1);

        static bool _cronWork = false;

        async static void Cron(object state)
        {
            if (_cronWork || links.Count == 0)
                return;

            _cronWork = true;

            try
            {
                if (cronRound == 60)
                {
                    cronRound = 0;
                    tempLinks.Clear();
                }

                cronRound++;

                using (var sqlDb = new ProxyLinkContext())
                {
                    if (DateTime.Now > _nextClearDb)
                    {
                        _nextClearDb = DateTime.Now.AddHours(1);

                        var now = DateTime.Now;

                        await sqlDb.links
                             .Where(i => now > i.ex)
                             .ExecuteDeleteAsync();
                    }
                    else
                    {
                        foreach (var link in links.ToArray())
                        {
                            try
                            {
                                if (AppInit.conf.mikrotik || link.Value.proxy != null || DateTime.Now.AddMinutes(5) > link.Value.ex || link.Value.uri.Contains(" or "))
                                {
                                    if (DateTime.Now > link.Value.ex)
                                        links.TryRemove(link.Key, out _);
                                }
                                else
                                {
                                    if (tempLinks.Contains(link.Key))
                                        links.TryRemove(link.Key, out _);
                                    else
                                    {
                                        link.Value.id = link.Key;

                                        await sqlDb.links
                                            .Where(x => x.Id == link.Key)
                                            .ExecuteDeleteAsync();

                                        sqlDb.links.Add(new ProxyLinkSqlModel()
                                        {
                                            Id = link.Key,
                                            ex = link.Value.ex,
                                            json = JsonSerializer.Serialize(link.Value)
                                        });

                                        if (await sqlDb.SaveChangesAsync() > 0)
                                        {
                                            tempLinks.Add(link.Key);
                                            links.TryRemove(link.Key, out _);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex) { Console.WriteLine($"ProxyLink: {ex}"); }
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"ProxyLink: {ex}"); }
            finally 
            {
                _cronWork = false;
            }
        }
        #endregion
    }
}
