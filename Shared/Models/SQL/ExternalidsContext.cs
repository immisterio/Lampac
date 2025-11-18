using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Threading;

namespace Shared.Models.SQL
{
    public partial class ExternalidsContext
    {
        public static readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        public static void Initialization() 
        {
            try
            {
                var sqlDb = new ExternalidsContext();
                    sqlDb.Database.EnsureCreated();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ExternalidsDb initialization failed: {ex.Message}");
            }
        }

        async public Task<int> SaveChangesLocks()
        {
            try
            {
                await semaphore.WaitAsync(TimeSpan.FromSeconds(30));

                return await base.SaveChangesAsync();
            }
            catch
            {
                return 0;
            }
            finally
            {
                semaphore.Release();
            }
        }
    }


    public partial class ExternalidsContext : DbContext
    {
        public DbSet<ExternalidsSqlModel> imdb { get; set; }

        public DbSet<ExternalidsSqlModel> kinopoisk { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(new SqliteConnectionStringBuilder
            {
                DataSource = "cache/Externalids.sql",
                Cache = SqliteCacheMode.Shared,
                DefaultTimeout = 10,
                Pooling = true
            }.ToString());

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
