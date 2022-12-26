using System.IO.Compression;
using System.IO;
using System.Text;

namespace Lampac.Engine.CORE
{
    public class BrotliTo
    {
        public static byte[] Compress(string value)
        {
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(value));
            using var output = new MemoryStream();
            using var stream = new BrotliStream(output, CompressionLevel.Fastest);

            input.CopyTo(stream);
            stream.Flush();

            return output.ToArray();
        }


        public static string Decompress(byte[] value)
        {
            using var input = new MemoryStream(value);
            using var output = new MemoryStream();
            using var stream = new BrotliStream(input, CompressionMode.Decompress);

            stream.CopyTo(output);
            stream.Flush();

            return Encoding.UTF8.GetString(output.ToArray());
        }
    }
}
