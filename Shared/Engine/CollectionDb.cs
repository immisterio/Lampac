using LiteDB;
using Shared.Models.Base;
using Shared.Models.Proxy;
using Shared.PlaywrightCore;
using System.Threading;

namespace Shared.Engine
{
    public static class CollectionDb
    {
        public static ILiteCollection<UserSync> sync_users { get; set; }

        public static ILiteCollection<Models.SISI.User> sisi_users { get; set; }

        public static ILiteCollection<HybridCacheModel> hybrid_cache { get; set; }

        public static ILiteCollection<BsonDocument> externalids_imdb { get; set; }

        public static ILiteCollection<BsonDocument> externalids_kp { get; set; }

        public static ILiteCollection<ProxyLinkModel> proxyLink { get; set; }

        public static ILiteCollection<PlaywrightRouteCache> playwrightRoute { get; set; }


        static LiteDatabase cacheDb, dataDb;

        public static void Configure()
        {
            try
            {
                dataDb = new LiteDatabase("database/app.db");
                sync_users = dataDb.GetCollection<UserSync>("sync_users");
                sisi_users = dataDb.GetCollection<Models.SISI.User>("sisi_users");
            }
            catch { }

            try
            {
                cacheDb = new LiteDatabase("cache/app.db");

                externalids_imdb = cacheDb.GetCollection("externalids_imdb");
                externalids_kp = cacheDb.GetCollection("externalids_kp");

                hybrid_cache = cacheDb.GetCollection<HybridCacheModel>("fdb");
                hybrid_cache.EnsureIndex(x => x.ex);

                proxyLink = cacheDb.GetCollection<ProxyLinkModel>("ProxyLink");
                proxyLink.EnsureIndex(x => x.ex);

                playwrightRoute = cacheDb.GetCollection<PlaywrightRouteCache>("playwrightRoute");
                playwrightRoute.EnsureIndex(x => x.ex);
            }
            catch { }

            ThreadPool.QueueUserWorkItem(async _ =>
            {
                while (true)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(1)).ConfigureAwait(false);
                        hybrid_cache.DeleteMany(i => DateTimeOffset.Now > i.ex);
                        proxyLink.DeleteMany(i => DateTimeOffset.Now > i.ex);
                        playwrightRoute.DeleteMany(i => DateTimeOffset.Now > i.ex);
                    }
                    catch { }
                }
            });
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
