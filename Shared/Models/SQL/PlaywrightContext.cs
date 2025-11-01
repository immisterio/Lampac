using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Shared.Models.SQL
{
    public static class PlaywrightDb
    {
        public static readonly PlaywrightContext Read, Write;

        static PlaywrightDb()
        {
            try
            {
                Write = new PlaywrightContext();
                Write.ChangeTracker.AutoDetectChangesEnabled = false;
                Write.Database.EnsureCreated();

                Read = new PlaywrightContext();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PlaywrightDb initialization failed: {ex.Message}");
            }
        }

        public static void Initialization() { }

        public static void FullDispose()
        {
            Read?.Dispose();
            Write?.Dispose();
        }
    }


    public class PlaywrightContext : DbContext
    {
        public DbSet<PlaywrightSqlModel> files { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=cache/Playwright.sql");
            optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProxyLinkSqlModel>()
                        .HasIndex(j => j.ex);
        }
    }

    public class PlaywrightSqlModel
    {
        [Key]
        public string Id { get; set; }

        public DateTime ex { get; set; }

        public byte[] content { get; set; }

        public string headers { get; set; }
    }
}
