using System.Buffers;
using System.Text;
using System.Text.Json;

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
        public static void WriteJson<T>(StringBuilder sb, in T value, JsonSerializerOptions options)
        {
            var bytes = new ArrayBufferWriter<byte>(512);

            using (var writer = new Utf8JsonWriter(bytes, new JsonWriterOptions
            {
                Indented = false,
                SkipValidation = true
            }))
            {
                JsonSerializer.Serialize(writer, value, options);
            }

            ReadOnlySpan<byte> utf8 = bytes.WrittenSpan;

            // Декодируем одним вызовом в pooled char[]
            int charCount = Encoding.UTF8.GetCharCount(utf8);
            char[] rented = ArrayPool<char>.Shared.Rent(charCount);

            try
            {
                int written = Encoding.UTF8.GetChars(utf8, rented);
                sb.Append(rented, 0, written);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
        #endregion
    }
}
