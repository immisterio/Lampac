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
                var sqlDb = new HybridCacheContext();
                    sqlDb.Database.EnsureCreated();
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
