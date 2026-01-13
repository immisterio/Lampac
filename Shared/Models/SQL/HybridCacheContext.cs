using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Shared.Models.SQL
{
    public partial class HybridCacheContext
    {
        public static IDbContextFactory<HybridCacheContext> Factory { get; set; }

        public static void Initialization()
        {
            Directory.CreateDirectory("cache");

            try
            {
                using (var sqlDb = new HybridCacheContext())
                {
                    sqlDb.Database.EnsureCreated();

                    using (var cmd = sqlDb.Database.GetDbConnection().CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA table_info('files');";

                        if (cmd.Connection.State != System.Data.ConnectionState.Open)
                            cmd.Connection.Open();

                        bool hasCapacity = false;

                        using (var reader = cmd.ExecuteReader())
                        {
                            int nameIndex = reader.GetOrdinal("name");

                            while (reader.Read())
                            {
                                var colName = reader.GetString(nameIndex);
                                if (string.Equals(colName, "capacity", StringComparison.OrdinalIgnoreCase))
                                {
                                    hasCapacity = true;
                                    break;
                                }
                            }
                        }

                        if (!hasCapacity)
                            sqlDb.Database.ExecuteSqlRaw("ALTER TABLE files ADD COLUMN capacity INTEGER NOT NULL DEFAULT 0;");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HybridCacheDb initialization failed: {ex.Message}");
            }
        }

        public static void ConfiguringDbBuilder(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite(new SqliteConnectionStringBuilder
                {
                    DataSource = "cache/HybridCache.sql",
                    Cache = SqliteCacheMode.Shared,
                    DefaultTimeout = 10,
                    Pooling = true
                }.ToString());

                optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            }
        }
    }


    public partial class HybridCacheContext : DbContext
    {
        public DbSet<HybridCacheSqlModel> files { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            ConfiguringDbBuilder(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HybridCacheSqlModel>()
                        .HasIndex(j => j.ex);
        }
    }


    public class HybridCacheSqlModel
    {
        [Key]
        public string Id { get; set; }

        public DateTime ex { get; set; }

        public string value { get; set; }

        public int capacity { get; set; }
    }
}
