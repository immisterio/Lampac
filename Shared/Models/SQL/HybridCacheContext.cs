using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Models.SQL
{
    public class HybridCacheContext : DbContext
    {
        public static void Configure()
        {
            try
            {
                using (var context = new HybridCacheContext())
                    context.Database.EnsureCreated();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HybridCache.sql initialization failed: {ex.Message}");
            }
        }

        public DbSet<HybridCacheSqlModel> files { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (Startup.IsShutdown)
                return;

            optionsBuilder.UseSqlite("Data Source=cache/HybridCache.sql");
            //optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }

        //protected override void OnModelCreating(ModelBuilder modelBuilder)
        //{
        //    modelBuilder.Entity<HybridCacheSqlModel>()
        //                .HasIndex(j => j.ex);
        //}
    }

    public class HybridCacheSqlModel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; }

        public DateTime ex { get; set; }

        public string value { get; set; }
    }
}
