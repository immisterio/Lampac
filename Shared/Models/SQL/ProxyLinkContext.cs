using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Models.SQL
{
    public class ProxyLinkContext : DbContext
    {
        public static void Configure()
        {
            try
            {
                using (var context = new ProxyLinkContext())
                    context.Database.EnsureCreated();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ProxyLink.sql initialization failed: {ex.Message}");
            }
        }

        public DbSet<ProxyLinkSqlModel> links { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=cache/ProxyLink.sql;Pooling=true;");
            //optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
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
