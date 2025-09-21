using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Models.SQL
{
    public class PlaywrightContext : DbContext
    {
        public static void Configure()
        {
            try
            {
                using (var context = new PlaywrightContext())
                    context.Database.EnsureCreated();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Playwright.sql initialization failed: {ex.Message}");
            }
        }

        public DbSet<PlaywrightSqlModel> files { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (Startup.IsShutdown)
                return;

            optionsBuilder.UseSqlite("Data Source=cache/Playwright.sql");
            //optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }

        //protected override void OnModelCreating(ModelBuilder modelBuilder)
        //{
        //    modelBuilder.Entity<ProxyLinkSqlModel>()
        //                .HasIndex(j => j.ex);
        //}
    }

    public class PlaywrightSqlModel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; }

        public DateTime ex { get; set; }

        public byte[] content { get; set; }

        public string headers { get; set; }
    }
}
