using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Shared.Models.SQL
{
    public partial class ProxyLinkContext
    {
        public static ProxyLinkContext Read { get; private set; }

        public static void Initialization() 
        {
            try
            {
                var sqlDb = new ProxyLinkContext();
                    sqlDb.Database.EnsureCreated();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ProxyLinkDb initialization failed: {ex.Message}");
            }
        }
    }


    public partial class ProxyLinkContext : DbContext
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
        public string Id { get; set; }

        public DateTime ex { get; set; }

        public string json { get; set; }
    }
}
