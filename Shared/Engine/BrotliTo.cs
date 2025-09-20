using System.IO.Compression;
using System.Text;

namespace Shared.Engine
{
    public static class BrotliTo
    {
        static readonly object lockObj = new object();


        public static byte[] Compress(string value)
        {
            return Compress(Encoding.UTF8.GetBytes(value));
        }

        public static byte[] Compress(in byte[] value)
        {
            try
            {
                using (var input = new MemoryStream(value))
                {
                    using (var output = new MemoryStream())
                    {
                        try
                        {
                            using (var stream = new BrotliStream(output, CompressionLevel.Fastest))
                                input.CopyTo(stream);

                            return output.ToArray();
                        }
                        catch { return null; }
                    }
                }
            }
            catch { return null; }
        }

        public static void Compress(string outfile, in string value)
        {
            try
            {
                Compress(outfile, Encoding.UTF8.GetBytes(value));
            }
            catch { }
        }

        public static void Compress(string outfile, in byte[] value)
        {
            try
            {
                lock (lockObj)
                {
                    using (var input = new MemoryStream(value))
                    {
                        using (var output = new FileStream(outfile, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            try
                            {
                                using (var stream = new BrotliStream(output, CompressionLevel.Fastest))
                                    input.CopyTo(stream);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        }


        public static string Decompress(byte[] value)
        {
            try
            {
                using (var input = new MemoryStream(value))
                {
                    using (var output = new MemoryStream())
                    {
                        try
                        {
                            using (var stream = new BrotliStream(input, CompressionMode.Decompress))
                                stream.CopyTo(output);

                            return Encoding.UTF8.GetString(output.ToArray());
                        }
                        catch { return null; }
                    }
                }
            }
            catch { return null; }
        }

        public static string Decompress(string infile)
        {
            try
            {
                byte[] array = DecompressArray(infile);
                if (array == null)
                    return null;

                return Encoding.UTF8.GetString(array);
            }
            catch { return null; }
        }

        public static byte[] DecompressArray(string infile)
        {
            try
            {
                using (var input = new FileStream(infile, FileMode.Open, FileAccess.Read))
                {
                    using (var output = new MemoryStream())
                    {
                        try
                        {
                            using (var stream = new BrotliStream(input, CompressionMode.Decompress))
                                stream.CopyTo(output);

                            return output.ToArray();
                        }
                        catch { return null; }
                    }
                }
            }
            catch { return null; }
        }
    }
}
