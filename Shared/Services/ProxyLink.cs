using Shared.Models.Proxy;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Shared.Services;

public class ProxyLink : IProxyLink
{
    #region static
    [ThreadStatic]
    private static StringBuilder _threadHashBuilder;

    [ThreadStatic]
    private static ArrayBufferWriter<byte> _threadBufferWriter;

    static readonly ConcurrentDictionary<string, ProxyLinkModel> links = new();

    static readonly Timer _cronTimer = new Timer(Cron, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

    public static int Stat_ContLinks
        => links.IsEmpty ? 0 : links.Count;

    static readonly JsonWriterOptions _jsonWriterOptions = new JsonWriterOptions
    {
        Indented = false,
        SkipValidation = true
    };
    #endregion


    #region Encrypt
    public string Encrypt(ReadOnlySpan<char> uri, string plugin, DateTime ex = default, bool IsProxyImg = false)
        => Encrypt(uri, null, verifyip: false, ex: ex, plugin: plugin, IsProxyImg: IsProxyImg);

    public static string Encrypt(ReadOnlySpan<char> uri, ProxyLinkModel p, bool forceMd5 = false, string[] prefix = null, Action<StringBuilder> sbWriter = null)
        => Encrypt(uri, p.reqip, p.headers, p.proxy, p.plugin, p.verifyip, default, p.md5 || forceMd5, false, prefix, p.userdata, sbWriter);

    public static string Encrypt(ReadOnlySpan<char> uri, string reqip, IReadOnlyList<HeadersModel> headers = null, WebProxy proxy = null, string plugin = null, bool verifyip = true, DateTime ex = default, bool forceMd5 = false, bool IsProxyImg = false, string[] prefix = null, object userdata = null, Action<StringBuilder> sbWriter = null)
    {
        if (uri.IsEmpty)
            return string.Empty;

        StringBuilder hash = _threadHashBuilder ??= new StringBuilder(2048);
        hash.Clear();

        if (prefix != null)
        {
            foreach (string pfx in prefix)
            {
                if (pfx != null)
                    hash.Append(pfx);
            }
        }

        int sharpIndex = uri.IndexOf('#');
        ReadOnlySpan<char> uri_clear = sharpIndex >= 0
            ? uri[..sharpIndex].Trim()
            : uri.Trim();

        if (plugin == "posterapi")
        {
            return SerializePayload(hash, IsProxyImg, uri_clear, uri, plugin, null, false, default, null, sbWriter);
        }
        else if (!forceMd5 && proxy == null && userdata == null && !uri_clear.Contains(" or ", StringComparison.Ordinal))
        {
            return verifyip && CoreInit.conf.serverproxy.verifyip
                ? SerializePayload(hash, IsProxyImg, uri_clear, uri, plugin, reqip, true, DateTime.UtcNow.Date.AddDays(1), headers, sbWriter)
                : SerializePayload(hash, IsProxyImg, uri_clear, uri, plugin, null, false, default, headers, sbWriter);
        }
        else
        {
            string uclear = uri_clear.ToString();
            string md5key = CrypTo.md5(verifyip && CoreInit.conf.serverproxy.verifyip
                ? uclear + reqip
                : uclear
            );

            string ext = GetExtension(uri, IsProxyImg);

            hash.Append(md5key);
            hash.Append(ext);

            links[md5key + ext] = new ProxyLinkModel(verifyip ? reqip : null, headers, proxy, uclear, plugin, verifyip, ex, userdata)
            {
                md5 = true
            };

            if (sbWriter != null)
                sbWriter.Invoke(hash);

            return hash.ToString();
        }
    }
    #endregion

    #region SerializePayload
    static string SerializePayload(StringBuilder sbhash, bool isProxyImg, ReadOnlySpan<char> uri_clear, ReadOnlySpan<char> uri, string plugin, string reqip, bool verifyip, DateTime e, IReadOnlyList<HeadersModel> headers, Action<StringBuilder> sbWriter)
    {
        _threadBufferWriter ??= new ArrayBufferWriter<byte>(4096);
        _threadBufferWriter.ResetWrittenCount();

        #region serialize
        using (var writer = new Utf8JsonWriter(_threadBufferWriter, _jsonWriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("u"u8, uri_clear);

            if (plugin != null)
                writer.WriteString("p"u8, plugin);

            if (reqip != null)
                writer.WriteString("i"u8, reqip);

            if (verifyip)
                writer.WriteBoolean("v"u8, true);

            if (e != default)
                writer.WriteString("e"u8, e.ToUniversalTime());

            if (headers != null && headers.Count > 0)
            {
                // ставим выше h что бы в Decrypt успеть считать количество заголовков до их чтения
                writer.WriteNumber("hc"u8, headers.Count);

                writer.WritePropertyName("h"u8);
                writer.WriteStartObject();

                foreach (var h in headers)
                {
                    if (h.name != null && h.val != null)
                        writer.WriteString(h.name, h.val);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        // JSON уже в UTF-8, перекодировать не нужно
        ReadOnlySpan<byte> json = _threadBufferWriter.WrittenSpan;

        if (json.IsEmpty)
            return "Error Serialize Payload: jsonUtf8";
        #endregion

        #region AES Encrypt
        try
        {
            var aesinst = AesPool.Instance;

            // EncryptCbc сам делает PKCS7 padding, paddedLen считать не нужно
            // destination должен быть достаточно большой: json.Length + blockSize
            int blockSize = aesinst.Aes.BlockSize / 8; // 16
            int requiredCipherLen = ((json.Length / blockSize) + 1) * blockSize;

            BufferBytePool destBuf = null;
            if (requiredCipherLen > AesInstance.ByteSize)
                destBuf = new BufferBytePool(requiredCipherLen);

            try
            {
                Span<byte> dest = destBuf != null
                    ? destBuf.Span
                    : aesinst.ByteBuffer;

                int cipherLen = aesinst.Aes.EncryptCbc(
                    json,
                    aesinst.Aes.IV, // iv (16 байт)
                    dest,
                    PaddingMode.PKCS7);

                if (cipherLen <= 0)
                    return "Error Serialize Payload: cipherLen";

                int capacity = ((cipherLen + 2) / 3) * 4;

                BufferCharPool base64Chars = null;
                if (capacity > AesInstance.CharSize)
                    base64Chars = new BufferCharPool(capacity);

                try
                {
                    Span<char> base64 = base64Chars != null
                        ? base64Chars.Span
                        : aesinst.CharBuffer;

                    if (!Base64Url.TryEncodeToChars(dest.Slice(0, cipherLen), base64, out int charsWritten) || charsWritten <= 0)
                        return "Error Serialize Payload: base64Chars";

                    base64 = base64.Slice(0, charsWritten);

                    sbhash.Append(base64);
                    sbhash.Append(GetExtension(uri, isProxyImg));

                    if (sbWriter != null)
                    {
                        sbWriter.Invoke(sbhash);
                        return null;
                    }

                    return sbhash.ToString();
                }
                finally
                {
                    base64Chars?.Dispose();
                }
            }
            finally
            {
                destBuf?.Dispose();
            }
        }
        catch
        {
            return "Error Serialize Payload: Exception";
        }
        #endregion
    }
    #endregion

    #region GetExtension
    static string GetExtension(ReadOnlySpan<char> uri, bool IsProxyImg)
    {
        ReadOnlySpan<char> ext = default;
        int end = uri.LastIndexOf('#');

        if (end > 0 && uri.Length > end + 1)
        {
            // #.m3u8, #.jpg и т.д
            ext = uri.Slice(end + 1);
        }
        else
        {
            end = uri.IndexOfAny('?', '&');
            if (end >= 0)
                uri = uri[..end];

            int dot = uri.LastIndexOf('.');
            if (dot < 0)
            {
                if (IsProxyImg)
                    return ".jpg"; // по умолчанию для картинок .jpg

                return string.Empty;
            }

            ext = uri[dot..];
        }

        if (IsProxyImg)
        {
            return ext switch
            {
                ".png" => ".png",
                ".webp" => ".webp",
                _ => ".jpg"
            };
        }
        else
        {
            return ext switch
            {
                ".m3u8" => ".m3u8",
                ".m3u" => ".m3u",
                ".mpd" => ".mpd",
                ".webm" => ".webm",
                ".ts" => ".ts",
                ".m4s" => ".m4s",
                ".mp4" => ".mp4",
                ".mov" => ".mov",
                ".mkv" => ".mkv",
                ".aac" => ".aac",
                ".vtt" => ".vtt",
                ".srt" => ".srt",
                ".jpg" or ".jpeg" => ".jpg",
                ".png" => ".png",
                ".webp" => ".webp",
                _ => string.Empty
            };
        }
    }
    #endregion


    #region Decrypt
    public static ProxyLinkModel Decrypt(ReadOnlySpan<char> hash, string reqip)
    {
        if (hash.IsEmpty)
            return null;

        try
        {
            if (IsAes(hash))
            {
                ReadOnlySpan<char> base64hash = hash;
                int dot = hash.LastIndexOf('.');
                if (dot > 0)
                    base64hash = base64hash.Slice(0, dot);

                var aesinst = AesPool.Instance;

                int capacity = ((base64hash.Length + 3) / 4) * 3;

                BufferBytePool cipherBuf = null;
                if (capacity > AesInstance.ByteSize)
                    cipherBuf = new BufferBytePool(capacity);

                try
                {
                    Span<byte> cipher = cipherBuf != null
                        ? cipherBuf.Span
                        : aesinst.ByteBuffer;

                    if (!Base64Url.TryDecodeFromChars(base64hash, cipher, out int cipherLen) || cipherLen <= 0)
                        return null;

                    BufferBytePool destBuf = null;
                    if (cipherLen > AesInstance.ByteSize)
                        destBuf = new BufferBytePool(cipherLen);

                    try
                    {
                        Span<byte> dest = destBuf != null
                            ? destBuf.Span
                            : aesinst.DestBuffer;

                        int plainLen = aesinst.Aes.DecryptCbc(
                            cipher.Slice(0, cipherLen),
                            aesinst.Aes.IV,
                            dest,
                            PaddingMode.PKCS7);

                        if (plainLen <= 0)
                            return null;

                        return ReadAesPayload(dest.Slice(0, plainLen), reqip);
                    }
                    finally
                    {
                        destBuf?.Dispose();
                    }
                }
                finally
                {
                    cipherBuf?.Dispose();
                }
            }
            else
            {
                if (links.TryGetValue(hash.ToString(), out ProxyLinkModel val))
                {
                    if (val.verifyip == false || CoreInit.conf.serverproxy.verifyip == false || val.reqip == string.Empty || reqip == null || reqip == val.reqip)
                        return val;
                }

                return null;
            }
        }
        catch
        {
            return null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static ProxyLinkModel ReadAesPayload(ReadOnlySpan<byte> json, string reqip)
    {
        var reader = new Utf8JsonReader(json);

        short headersCount = 0;
        string uri_clear = null, plugin = null, ip = null;
        bool verifyip = false;
        DateTime e = default;
        List<HeadersModel> headers = null;

        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            if (reader.ValueTextEquals("u"u8))
            {
                reader.Read();
                uri_clear = reader.GetString();
            }
            else if (reader.ValueTextEquals("p"u8))
            {
                reader.Read();
                plugin = reader.GetString();
            }
            else if (reader.ValueTextEquals("i"u8))
            {
                reader.Read();
                ip = reader.GetString();
            }
            else if (reader.ValueTextEquals("v"u8))
            {
                reader.Read();
                verifyip = reader.GetBoolean();
            }
            else if (reader.ValueTextEquals("e"u8))
            {
                reader.Read();
                e = reader.GetDateTime();
            }
            else if (reader.ValueTextEquals("hc"u8))
            {
                reader.Read();
                headersCount = reader.GetInt16();
            }
            else if (reader.ValueTextEquals("h"u8))
            {
                reader.Read();

                headers = new List<HeadersModel>(headersCount);

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    string name = reader.GetString();
                    reader.Read();
                    string val = reader.GetString();

                    if (name != null && val != null)
                        headers.Add(new(name, val));
                }
            }
            else
            {
                reader.Skip();
            }
        }

        if (uri_clear == null)
            return null;

        if (verifyip)
        {
            if (reqip != null && ip != reqip)
                return null;

            if (DateTime.UtcNow > e)
                return null;
        }

        return new ProxyLinkModel(reqip, headers, null, uri_clear, plugin, verifyip, e);
    }
    #endregion

    #region IsAes
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool IsAes(ReadOnlySpan<char> hash)
    {
        if (hash.IsEmpty)
            return false;

        if (hash.Length >= 4 &&
           (hash[0] == 'h' || hash[0] == 'H') &&
           (hash[1] == 't' || hash[1] == 'T') &&
           (hash[2] == 't' || hash[2] == 'T') &&
           (hash[3] == 'p' || hash[3] == 'P'))
        {
            // прямая ссылка tmdb/cub proxy
            return false;
        }

        int idx = hash.IndexOfAny('?', '&', '.');
        int len = idx >= 0 ? idx : hash.Length;

        /// если длина 32 — это MD5 proxy
        /// остальное AES proxy
        return len != 32;
    }
    #endregion


    #region Cron
    static int _updatingDb = 0;

    static void Cron(object state)
    {
        if (links.IsEmpty)
            return;

        if (Interlocked.Exchange(ref _updatingDb, 1) == 1)
            return;

        try
        {
            var now = DateTime.UtcNow;

            foreach (var link in links)
            {
                if (link.Value.ex != default && now > link.Value.ex)
                    links.TryRemove(link.Key, out _);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "{Class} {CatchId}", "ProxyLink", "id_s1dvs8n1");
        }
        finally
        {
            Volatile.Write(ref _updatingDb, 0);
        }
    }
    #endregion
}
