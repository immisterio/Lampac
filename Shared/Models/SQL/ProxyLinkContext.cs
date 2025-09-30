using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Models.SQL
{
    public static class ProxyLinkDb
    {
        public static readonly ProxyLinkContext Read, Write;

        static ProxyLinkDb()
        {
            try
            {
                Write = new ProxyLinkContext();
                Write.ChangeTracker.AutoDetectChangesEnabled = false;
                Write.Database.EnsureCreated();

                Read = new ProxyLinkContext();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ProxyLinkDb initialization failed: {ex.Message}");
            }
        }

        public static void FullDispose()
        {
            Read?.Dispose();
            Write?.Dispose();
        }
    }


    public class ProxyLinkContext : DbContext
    {
        public DbSet<ProxyLinkSqlModel> links { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=cache/ProxyLink.sql");
            optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProxyLinkSqlModel>()
                        .HasIndex(j => j.ex);
        }
    }

    public class ProxyLinkSqlModel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; }

        public DateTime ex { get; set; }

        public string json { get; set; }
    }
}
