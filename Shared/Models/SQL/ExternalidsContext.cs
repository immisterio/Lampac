using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Shared.Models.SQL
{
    public static class ExternalidsDb
    {
        public static ExternalidsContext Read { get; private set; }

        public static void Initialization() 
        {
            try
            {
                var sqlDb = new ExternalidsContext();
                sqlDb.Database.EnsureCreated();

                Read = sqlDb;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ExternalidsDb initialization failed: {ex.Message}");
            }
        }

        public static void FullDispose()
        {
            Read?.Dispose();
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
