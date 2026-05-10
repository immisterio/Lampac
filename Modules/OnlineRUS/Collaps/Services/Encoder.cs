using Shared.Models.Events;
using Shared.Services.Pools;
using Shared.Services.Utilities;
using System;
using System.Threading.Tasks;

namespace Collaps;

public static class Encoder
{
    const string marker = "/x-en-x/";

    async public static Task ProxyApiCreateHttpRequest(EventProxyApiCreateHttpRequest e)
    {
        if (e.plugin != null && e.plugin.Equals("collaps", StringComparison.OrdinalIgnoreCase))
        {
            if (!e.requestMessage.RequestUri.AbsolutePath.Contains(marker))
            {
                string newUri = EncodeUri(e.requestMessage.RequestUri);
                if (newUri != null)
                    e.requestMessage.RequestUri = new Uri(newUri);
            }
        }
    }

    public static string Uri(string url)
    {
        if (string.IsNullOrEmpty(url) || url.Contains(marker))
            return url;

        string newUri = EncodeUri(new Uri(url));
        if (newUri == null)
            return url;

        return newUri + (url.Contains(".vtt") ? "#.vtt" : url.Contains(".mpd") ? "#.mpd" : "#.m3u8");
    }

    static string EncodeUri(Uri uri)
    {
        long n = (long)Math.Round(
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 / 60 / 60
        );

        string newUri = null;

        CrypTo.Base64($"{n}/{uri.AbsolutePath}{uri.Query}", base64 =>
        {
            var sb = StringBuilderPool.ThreadInstance;

            sb.Append(uri.Scheme)
              .Append("://")
              .Append(uri.Authority)
              .Append(marker);

            for (int i = 0; i < base64.Length; i++)
            {
                char c = base64[i];

                sb.Append(c switch
                {
                    'A' => 'D',
                    'B' => 'l',
                    'C' => 'C',
                    'D' => 'h',
                    'E' => 'E',
                    'F' => 'X',
                    'G' => 'i',
                    'H' => 't',
                    'I' => 'L',
                    'J' => 'O',
                    'K' => 'N',
                    'L' => 'Y',
                    'M' => 'R',
                    'N' => 'k',
                    'O' => 'F',
                    'P' => 'j',
                    'Q' => 'A',
                    'R' => 's',
                    'S' => 'n',
                    'T' => 'B',
                    'U' => 'b',
                    'V' => 'y',
                    'W' => 'm',
                    'X' => 'W',
                    'Y' => 'z',
                    'Z' => 'S',
                    'a' => 'H',
                    'b' => 'M',
                    'c' => 'q',
                    'd' => 'K',
                    'e' => 'P',
                    'f' => 'g',
                    'g' => 'Q',
                    'h' => 'Z',
                    'i' => 'p',
                    'j' => 'v',
                    'k' => 'w',
                    'l' => 'e',
                    'm' => 'r',
                    'n' => 'o',
                    'o' => 'f',
                    'p' => 'J',
                    'q' => 'T',
                    'r' => 'V',
                    's' => 'd',
                    't' => 'I',
                    'u' => 'u',
                    'v' => 'U',
                    'w' => 'c',
                    'x' => 'x',
                    'y' => 'a',
                    'z' => 'G',
                    _ => c
                });
            }

            newUri = sb.ToString();
        });

        return newUri;
    }
}
