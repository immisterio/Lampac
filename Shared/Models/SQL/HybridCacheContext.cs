using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Shared.Models.SQL
{
    public static class HybridCacheDb
    {
        public static readonly HybridCacheContext Read, Write;

        static HybridCacheDb()
        {
            try
            {
                Write = new HybridCacheContext();
                Write.ChangeTracker.AutoDetectChangesEnabled = false;
                Write.Database.EnsureCreated();

                Read = new HybridCacheContext();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HybridCacheDb initialization failed: {ex.Message}");
            }
        }

        public static void FullDispose()
        {
            Read?.Dispose();
            Write?.Dispose();
        }
    }


    public class HybridCacheContext : DbContext
    {
        public DbSet<HybridCacheSqlModel> files { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=cache/HybridCache.sql");
            optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
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
    }
}
