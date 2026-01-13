using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Shared.Models.SQL
{
    public partial class ProxyLinkContext
    {
        public static IDbContextFactory<ProxyLinkContext> Factory { get; set; }

        public static void Initialization() 
        {
            Directory.CreateDirectory("cache");

            try
            {
                using (var sqlDb = new ProxyLinkContext())
                    sqlDb.Database.EnsureCreated();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ProxyLinkDb initialization failed: {ex.Message}");
            }
        }

        public static void ConfiguringDbBuilder(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite(new SqliteConnectionStringBuilder
                {
                    DataSource = "cache/ProxyLink.sql",
                    Cache = SqliteCacheMode.Shared,
                    DefaultTimeout = 10,
                    Pooling = true
                }.ToString());

                optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            }
        }
    }


    public partial class ProxyLinkContext : DbContext
    {
        public DbSet<ProxyLinkSqlModel> links { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            ConfiguringDbBuilder(optionsBuilder);
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
