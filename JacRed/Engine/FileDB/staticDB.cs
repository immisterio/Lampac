using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Jackett;
using JacRed.Engine.CORE;
using JacRed.Models;
using JacRed.Models.Details;
using Lampac.Engine.CORE;

namespace JacRed.Engine
{
    public partial class FileDB : IDisposable
    {
        #region FileDB
        /// <summary>
        /// $"{search_name}:{search_originalname}"
        /// Верхнее время изменения 
        /// </summary>
        public static Dictionary<string, DateTime> masterDb = new Dictionary<string, DateTime>();

        static ConcurrentDictionary<string, WriteTaskModel> openWriteTask = new ConcurrentDictionary<string, WriteTaskModel>();

        static FileDB()
        {
            if (File.Exists("cache/jacred/masterDb.bz"))
                masterDb = JsonStream.Read<Dictionary<string, DateTime>>("cache/jacred/masterDb.bz");
        }
        #endregion

        #region pathDb
        static string pathDb(string key)
        {
            string md5key = CrypTo.md5(key);

            Directory.CreateDirectory($"cache/jacred/fdb/{md5key.Substring(0, 2)}");
            return $"cache/jacred/fdb/{md5key.Substring(0, 2)}/{md5key.Substring(2)}";
        }
        #endregion

        #region OpenRead / OpenWrite
        public static IReadOnlyDictionary<string, TorrentDetails> OpenRead(string key)
        {
            if (openWriteTask.TryGetValue(key, out WriteTaskModel val))
                return val.db.Database;

            if (ModInit.conf.evercache)
            {
                var fdb = new FileDB(key);
                openWriteTask.TryAdd(key, new WriteTaskModel() { db = fdb, openconnection = 1 });
                return fdb.Database;
            }

            return new FileDB(key).Database;
        }

        public static FileDB OpenWrite(string key)
        {
            if (openWriteTask.TryGetValue(key, out WriteTaskModel val))
            {
                val.openconnection += 1;
                return val.db;
            }
            else
            {
                var fdb = new FileDB(key, empty: true);
                openWriteTask.TryAdd(key, new WriteTaskModel() { db = fdb, openconnection = 1 });
                return fdb;
            }
        }
        #endregion

        #region SaveChangesToFile
        public static void SaveChangesToFile()
        {
            try
            {
                JsonStream.Write("cache/jacred/masterDb.bz", masterDb);

                if (!File.Exists($"cache/jacred/masterDb_{DateTime.Today:dd-MM-yyyy}.bz"))
                    File.Copy("cache/jacred/masterDb.bz", $"cache/jacred/masterDb_{DateTime.Today:dd-MM-yyyy}.bz");

                if (File.Exists($"cache/jacred/masterDb_{DateTime.Today.AddDays(-2):dd-MM-yyyy}.bz"))
                    File.Delete($"cache/jacred/masterDb_{DateTime.Today.AddDays(-2):dd-MM-yyyy}.bz");
            }
            catch { }
        }
        #endregion
    }
}
