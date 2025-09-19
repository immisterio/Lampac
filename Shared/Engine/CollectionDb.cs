using LiteDB;
using Shared.Models.Base;
using Shared.PlaywrightCore;
using System.Threading;

namespace Shared.Engine
{
    public static class CollectionDb
    {
        #region static
        public static ILiteCollection<UserSync> sync_users { get; set; }

        public static ILiteCollection<Models.SISI.User> sisi_users { get; set; }

        public static ILiteCollection<HybridCacheModel> hybrid_cache { get; set; }

        public static ILiteCollection<BsonDocument> externalids_imdb { get; set; }

        public static ILiteCollection<BsonDocument> externalids_kp { get; set; }


        static LiteDatabase cacheDb, dataDb;
        static Timer _clearTimer;
        #endregion

        public static void Configure()
        {
            try
            {
                dataDb = new LiteDatabase("database/app.db");
                sync_users = dataDb.GetCollection<UserSync>("sync_users");
                sisi_users = dataDb.GetCollection<Models.SISI.User>("sisi_users");
            }
            catch (Exception ex) { Console.WriteLine(ex); }

            try
            {
                cacheDb = new LiteDatabase("cache/app.db");

                externalids_imdb = cacheDb.GetCollection("externalids_imdb");
                externalids_kp = cacheDb.GetCollection("externalids_kp");

                hybrid_cache = cacheDb.GetCollection<HybridCacheModel>("fdb");
                hybrid_cache.EnsureIndex(x => x.ex);
            }
            catch (Exception ex) { Console.WriteLine("CollectionDb: " + ex); }

            _clearTimer = new Timer(ClearDB, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }


        static bool workClearDB = false;
        static void ClearDB(object state)
        {
            if (workClearDB)
                return;

            try
            {
                workClearDB = true;

                var ex = DateTimeOffset.Now;
                hybrid_cache?.DeleteMany(i => ex > i.ex);
            }
            catch { }

            workClearDB = false;
        }

        public static void Dispose()
        {
            try
            {
                dataDb?.Dispose();
                cacheDb?.Dispose();
            }
            catch { }
        }
    }
}
