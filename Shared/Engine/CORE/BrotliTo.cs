﻿using System.IO.Compression;
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
            return Compress(Encoding.UTF8.GetBytes(value));
        }

        public static byte[] Compress(byte[] value)
        {
            try
            {
                using var input = new MemoryStream(value);
                using var output = new MemoryStream();
                using var stream = new BrotliStream(output, CompressionLevel.Fastest);

                input.CopyTo(stream);
                stream.Flush();

                return output.ToArray();
            }
            catch { return null; }
        }

        public static void Compress(string outfile, string value)
        {
            try
            {
                Compress(outfile, Encoding.UTF8.GetBytes(value));
            }
            catch { }
        }

        public static void Compress(string outfile, byte[] value)
        {
            try
            {
                lock (GetLockObjectForPath(outfile))
                {
                    using var input = new MemoryStream(value);
                    using var output = new FileStream(outfile, FileMode.Create);
                    using var stream = new BrotliStream(output, CompressionLevel.Fastest);

                    input.CopyTo(stream);
                    stream.Flush();
                }
            }
            catch { }
        }


        public static string Decompress(byte[] value)
        {
            try
            {
                using var input = new MemoryStream(value);
                using var output = new MemoryStream();
                using var stream = new BrotliStream(input, CompressionMode.Decompress);

                stream.CopyTo(output);
                stream.Flush();

                return Encoding.UTF8.GetString(output.ToArray());
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
                lock (GetLockObjectForPath(infile))
                {
                    using var input = new FileStream(infile, FileMode.Open);
                    using var output = new MemoryStream();
                    using var stream = new BrotliStream(input, CompressionMode.Decompress);

                    stream.CopyTo(output);
                    stream.Flush();

                    return output.ToArray();
                }
            }
            catch { return null; }
        }
    }
}
