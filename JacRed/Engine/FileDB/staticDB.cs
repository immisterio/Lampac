using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Jackett;
using JacRed.Engine.CORE;
using JacRed.Models;
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
        public static ConcurrentDictionary<string, DateTime> masterDb = new ConcurrentDictionary<string, DateTime>();

        static ConcurrentDictionary<string, WriteTaskModel> openWriteTask = new ConcurrentDictionary<string, WriteTaskModel>();

        static FileDB()
        {
            if (File.Exists("cache/jacred/masterDb.bz"))
                masterDb = JsonStream.Read<ConcurrentDictionary<string, DateTime>>("cache/jacred/masterDb.bz");

            if (masterDb == null)
            {
                if (File.Exists($"cache/jacred/masterDb_{DateTime.Today:dd-MM-yyyy}.bz"))
                    masterDb = JsonStream.Read<ConcurrentDictionary<string, DateTime>>($"cache/jacred/masterDb_{DateTime.Today:dd-MM-yyyy}.bz");

                if (masterDb == null && File.Exists($"cache/jacred/masterDb_{DateTime.Today.AddDays(-1):dd-MM-yyyy}.bz"))
                    masterDb = JsonStream.Read<ConcurrentDictionary<string, DateTime>>($"cache/jacred/masterDb_{DateTime.Today.AddDays(-1):dd-MM-yyyy}.bz");

                if (masterDb == null)
                    masterDb = new ConcurrentDictionary<string, DateTime>();

                if (File.Exists("cache/jacred/lastsync.txt"))
                    File.Delete("cache/jacred/lastsync.txt");
            }
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

        #region Open
        public static FileDB Open(string key, bool empty = false)
        {
            if (empty)
            {
                openWriteTask.TryRemove(key, out _);
                return new FileDB(key, empty: empty);
            }

            if (openWriteTask.TryGetValue(key, out WriteTaskModel val))
            {
                val.countread++;
                val.openconnection += 1;
                val.lastread = DateTime.UtcNow;
                return val.db;
            }
            else
            {
                var fdb = new FileDB(key);
                openWriteTask.TryAdd(key, new WriteTaskModel() { db = fdb, openconnection = 1, countread = 1, lastread = DateTime.UtcNow });
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


        #region Cron
        async public static Task Cron()
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(10));

                if (!ModInit.conf.Red.evercache.enable || 0 >= ModInit.conf.Red.evercache.validHour)
                    continue;

                try
                {
                    foreach (var i in openWriteTask)
                    {
                        if (DateTime.UtcNow > i.Value.lastread.AddHours(ModInit.conf.Red.evercache.validHour))
                            openWriteTask.TryRemove(i.Key, out _);
                    }
                }
                catch { }
            }
        }

        async public static Task CronFast()
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(20));

                if (!ModInit.conf.Red.evercache.enable || 0 >= ModInit.conf.Red.evercache.validHour)
                    continue;

                try
                {
                    if (openWriteTask.Count > ModInit.conf.Red.evercache.maxOpenWriteTask)
                    {
                        var query = openWriteTask.Where(i => DateTime.Now > i.Value.create.AddMinutes(10));
                        query = query.OrderBy(i => i.Value.countread).ThenBy(i => i.Value.lastread);

                        foreach (var i in query.Take(ModInit.conf.Red.evercache.dropCacheTake))
                            openWriteTask.TryRemove(i.Key, out _);
                    }
                }
                catch { }
            }
        }
        #endregion
    }
}
