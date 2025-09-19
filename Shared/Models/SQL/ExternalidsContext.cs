using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Models.SQL
{
    public class ExternalidsContext : DbContext
    {
        public static void Configure()
        {
            try
            {
                using (var context = new ExternalidsContext())
                    context.Database.EnsureCreated();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Externalids.sql initialization failed: {ex.Message}");
            }
        }

        public DbSet<ExternalidsSqlModel> imdb { get; set; }

        public DbSet<ExternalidsSqlModel> kinopoisk { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=cache/Externalids.sql");
            //optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }
    }

    public class ExternalidsSqlModel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; }

        public string value { get; set; }
    }
}
