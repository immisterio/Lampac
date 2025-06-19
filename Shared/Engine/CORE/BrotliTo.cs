using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Shared;

namespace Lampac.Engine.CORE
{
    public static class BrotliTo
    {
        static object GetLockObjectForPath(in string path)
        {
            lock (Startup.memoryCache)
            {
                string cacheKey = "BrotliTo:Lock_" + path;
                if (!Startup.memoryCache.TryGetValue(cacheKey, out object lockObj))
                {
                    lockObj = new object();
                    Startup.memoryCache.Set(cacheKey, lockObj, TimeSpan.FromSeconds(5));
                }

                return lockObj;
            }
        }


        public static byte[] Compress(in string value)
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
                        using (var stream = new BrotliStream(output, CompressionLevel.Fastest))
                            input.CopyTo(stream);

                        return output.ToArray();
                    }
                }
            }
            catch { return null; }
        }

        public static void Compress(in string outfile, in string value)
        {
            try
            {
                Compress(outfile, Encoding.UTF8.GetBytes(value));
            }
            catch { }
        }

        public static void Compress(in string outfile, in byte[] value)
        {
            try
            {
                lock (GetLockObjectForPath(outfile))
                {
                    using (var input = new MemoryStream(value))
                    {
                        using (var output = new FileStream(outfile, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            using (var stream = new BrotliStream(output, CompressionLevel.Fastest))
                                input.CopyTo(stream);
                        }
                    }
                }
            }
            catch { }
        }


        public static string Decompress(in byte[] value)
        {
            try
            {
                using (var input = new MemoryStream(value))
                {
                    using (var output = new MemoryStream())
                    {
                        using (var stream = new BrotliStream(input, CompressionMode.Decompress))
                            stream.CopyTo(output);

                        return Encoding.UTF8.GetString(output.ToArray());
                    }
                }
            }
            catch { return null; }
        }

        public static string Decompress(in string infile)
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

        public static byte[] DecompressArray(in string infile)
        {
            try
            {
                lock (GetLockObjectForPath(infile))
                {
                    using (var input = new FileStream(infile, FileMode.Open, FileAccess.Read))
                    {
                        using (var output = new MemoryStream())
                        {
                            using (var stream = new BrotliStream(input, CompressionMode.Decompress))
                                stream.CopyTo(output);

                            return output.ToArray();
                        }
                    }
                }
            }
            catch { return null; }
        }
    }
}
