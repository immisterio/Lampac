using LiteDB;
using Shared.Models.Base;

namespace Shared.Engine
{
    public static class CollectionDb
    {
        #region static
        public static ILiteCollection<UserSync> sync_users { get; set; }

        public static ILiteCollection<Models.SISI.User> sisi_users { get; set; }

        public static ILiteCollection<BsonDocument> externalids_imdb { get; set; }

        public static ILiteCollection<BsonDocument> externalids_kp { get; set; }


        static LiteDatabase cacheDb, dataDb;
        #endregion

        public static void Configure()
        {
            try
            {
                dataDb = new LiteDatabase("database/app.db");

                sync_users = dataDb.GetCollection<UserSync>("sync_users");
                sisi_users = dataDb.GetCollection<Models.SISI.User>("sisi_users");
                externalids_imdb = dataDb.GetCollection("externalids_imdb");
                externalids_kp = dataDb.GetCollection("externalids_kp");
            }
            catch (Exception ex) { Console.WriteLine("CollectionDb: " + ex); }

            if (File.Exists("cache/app.db"))
                File.Delete("cache/app.db");
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
