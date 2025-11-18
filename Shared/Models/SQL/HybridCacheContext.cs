using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Shared.Models.SQL
{
    public partial class HybridCacheContext
    {
        public static void Initialization() 
        {
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
    }


    public partial class HybridCacheContext : DbContext
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
