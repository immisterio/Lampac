using Shared.Engine.CORE;
using Shared.Model.Base;
using Shared.Model.Online;
using Shared.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lampac.Engine.CORE
{
    public class ProxyLink : IProxyLink
    {
        public string Encrypt(string uri, string plugin, DateTime ex = default) => Encrypt(uri, null, verifyip: false, ex: ex, plugin: plugin);


        static string conditionPath = "cache/proxylink.json";

        static ConcurrentDictionary<string, ProxyLinkModel> links = new ConcurrentDictionary<string, ProxyLinkModel>();

        static ProxyLink()
        {
            if (File.Exists(conditionPath))
            {
                try
                {
                    links = Newtonsoft.Json.JsonConvert.DeserializeObject<ConcurrentDictionary<string, ProxyLinkModel>>(BrotliTo.Decompress(conditionPath)) ?? new ConcurrentDictionary<string, ProxyLinkModel>();
                }
                catch { links = new ConcurrentDictionary<string, ProxyLinkModel>(); }
            }
        }

        public static string Encrypt(string uri, ProxyLinkModel p, bool forceMd5 = false) => Encrypt(uri, p.reqip, p.headers, p.proxy, p.plugin, forceMd5: forceMd5);

        public static string Encrypt(string uri, string reqip, List<HeadersModel> headers = null, WebProxy proxy = null, string plugin = null, bool verifyip = true, DateTime ex = default, bool forceMd5 = false)
        {
            if (string.IsNullOrWhiteSpace(uri))
                return string.Empty;

            string hash;
            bool IsMd5 = false;
            string uri_clear = uri.Split("#")[0].Trim();

            if (!forceMd5 && AppInit.conf.serverproxy.encrypt_aes && headers == null && proxy == null)
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

            if (IsMd5 && !links.ContainsKey(hash))
            {
                var md = new ProxyLinkModel(reqip, headers, proxy, uri_clear, plugin, verifyip, ex: ex);
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

            if (links.TryGetValue(hash, out ProxyLinkModel val))
            {
                if (!val.verifyip || !AppInit.conf.serverproxy.verifyip || reqip == null || reqip == val.reqip)
                    return val;
            }

            return null;
        }


        async public static Task Cron()
        {
            await Task.Delay(TimeSpan.FromMinutes(1)).ConfigureAwait(false);

            while (true)
            {
                try
                {
                    foreach (var link in links)
                    {
                        if (link.Value.ex != default)
                        {
                            if (DateTime.Now > link.Value.ex)
                                links.TryRemove(link.Key, out _);
                        }
                        else
                        {
                            if (DateTime.Now > link.Value.upd.AddHours(20))
                                links.TryRemove(link.Key, out _);
                        }
                    }

                    BrotliTo.Compress(conditionPath, Newtonsoft.Json.JsonConvert.SerializeObject(links));
                }
                catch { }

                await Task.Delay(TimeSpan.FromMinutes(2)).ConfigureAwait(false);
            }
        }
    }
}
