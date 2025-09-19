using LiteDB;

namespace Shared.Engine
{
    public static class CollectionDb
    {
        public static LiteDatabase Get() => new LiteDatabase("database/app.db");

        public static void Configure()
        {
            if (File.Exists("cache/app.db"))
                File.Delete("cache/app.db");
        }
    }
}
