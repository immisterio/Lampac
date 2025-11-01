using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Shared.Models.SQL
{
    public static class ExternalidsDb
    {
        public static readonly ExternalidsContext Read, Write;

        static ExternalidsDb()
        {
            try
            {
                Write = new ExternalidsContext();
                Write.ChangeTracker.AutoDetectChangesEnabled = false;
                Write.Database.EnsureCreated();

                Read = new ExternalidsContext();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ExternalidsDb initialization failed: {ex.Message}");
            }
        }

        public static void Initialization() { }

        public static void FullDispose()
        {
            Read?.Dispose();
            Write?.Dispose();
        }
    }


    public class ExternalidsContext : DbContext
    {
        public DbSet<ExternalidsSqlModel> imdb { get; set; }

        public DbSet<ExternalidsSqlModel> kinopoisk { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=cache/Externalids.sql");
            optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }
    }

    public class ExternalidsSqlModel
    {
        [Key]
        public string Id { get; set; }

        public string value { get; set; }
    }
}
