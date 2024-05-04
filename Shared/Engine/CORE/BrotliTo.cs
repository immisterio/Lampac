using System.IO.Compression;
using System.IO;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using System;
using Shared;

namespace Lampac.Engine.CORE
{
    public static class BrotliTo
    {
        static object GetLockObjectForPath(string path)
        {
            lock (Startup.memoryCache)
            {
                string cacheKey = "BrotliTo:Lock_" + path;
                if (!Startup.memoryCache.TryGetValue(cacheKey, out object lockObj))
                {
                    lockObj = new object();
                    Startup.memoryCache.Set(cacheKey, lockObj, TimeSpan.FromSeconds(10));
                }

                return lockObj;
            }
        }


        public static byte[] Compress(string value)
        {
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(value));
            using var output = new MemoryStream();
            using var stream = new BrotliStream(output, CompressionLevel.Fastest);

            input.CopyTo(stream);
            stream.Flush();

            return output.ToArray();
        }

        public static void Compress(string outfile, string value)
        {
            lock (GetLockObjectForPath(outfile))
            {
                using var input = new MemoryStream(Encoding.UTF8.GetBytes(value));
                using var output = new FileStream(outfile, FileMode.Create);
                using var stream = new BrotliStream(output, CompressionLevel.Fastest);

                input.CopyTo(stream);
                stream.Flush();
            }
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

        public static string Decompress(string infile)
        {
            lock (GetLockObjectForPath(infile))
            {
                using var input = new FileStream(infile, FileMode.Open);
                using var output = new MemoryStream();
                using var stream = new BrotliStream(input, CompressionMode.Decompress);

                stream.CopyTo(output);
                stream.Flush();

                return Encoding.UTF8.GetString(output.ToArray());
            }
        }
    }
}
