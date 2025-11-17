using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Shared.Models.SQL
{
    public partial class PlaywrightContext
    {
        public static PlaywrightContext Read { get; private set; }

        public static void Initialization() 
        {
            try
            {
                var sqlDb = new PlaywrightContext();
                    sqlDb.Database.EnsureCreated();

                Read = sqlDb;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PlaywrightDb initialization failed: {ex.Message}");
            }
        }

        public static void FullDispose()
        {
            Read?.Dispose();
        }
    }


    public partial class PlaywrightContext : DbContext
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
