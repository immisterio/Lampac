using Microsoft.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Shared.Models.Templates
{
    public static class UtilsTpl
    {
        #region HtmlEncode
        public static void HtmlEncode(ReadOnlySpan<char> value, StringBuilder sb)
        {
            foreach (var c in value)
            {
                switch (c)
                {
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '&': sb.Append("&amp;"); break;
                    case '"': sb.Append("&quot;"); break;
                    case '\'': sb.Append("&#39;"); break;
                    default: sb.Append(c); break;
                }
            }
        }
        #endregion

        #region WriteJson
        static readonly ThreadLocal<RecyclableMemoryStream> _msmJson = new(PoolInvk.msm.GetStream);

        static readonly char[] _rentedJson = new char[PoolInvk.rentLargeChunk];

        static readonly Decoder _decoderJson = Encoding.UTF8.GetDecoder();

        public static void WriteJson<T>(StringBuilder sb, in T value, JsonSerializerOptions options)
        {
            lock (_rentedJson)
            {
                var ms = _msmJson.Value;
                ms.SetLength(0);
                ms.Position = 0;

                using (var writer = new Utf8JsonWriter((Stream)ms, new JsonWriterOptions
                {
                    Indented = false,
                    SkipValidation = true
                }))
                {
                    JsonSerializer.Serialize(writer, value, options);
                }

                foreach (var segment in ms.GetReadOnlySequence())
                {
                    ReadOnlySpan<byte> bytes = segment.Span;

                    while (!bytes.IsEmpty)
                    {
                        _decoderJson.Convert(
                            bytes: bytes,
                            chars: _rentedJson,
                            flush: false,
                            out int bytesUsed,
                            out int charsUsed,
                            out _);

                        if (charsUsed > 0)
                            sb.Append(_rentedJson, 0, charsUsed);

                        bytes = bytes.Slice(bytesUsed);
                    }
                }

                // финальный flush
                _decoderJson.Convert(
                    bytes: ReadOnlySpan<byte>.Empty,
                    chars: _rentedJson,
                    flush: true,
                    out _,
                    out int finalCharsUsed,
                    out _);

                if (finalCharsUsed > 0)
                    sb.Append(_rentedJson, 0, finalCharsUsed);
            }
        }
        #endregion
    }
}
