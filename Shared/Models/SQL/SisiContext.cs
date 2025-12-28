using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Threading;

namespace Shared.Models.SQL
{
    public partial class SisiContext
    {
        public static readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        public static IDbContextFactory<SisiContext> Factory { get; set; }

        public static void Initialization() 
        {
            Directory.CreateDirectory("database");

            try
            {
                using (var sqlDb = new SisiContext())
                    sqlDb.Database.EnsureCreated();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SisiDb initialization failed: {ex.Message}");
            }
        }

        public static void ConfiguringDbBuilder(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite(new SqliteConnectionStringBuilder
                {
                    DataSource = "database/Sisi.sql",
                    Cache = SqliteCacheMode.Shared,
                    DefaultTimeout = 10,
                    Pooling = true
                }.ToString());

                optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
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


    public partial class SisiContext : DbContext
    {
        public DbSet<SisiBookmarkSqlModel> bookmarks { get; set; }

        public DbSet<SisiHistorySqlModel> historys { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            ConfiguringDbBuilder(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SisiBookmarkSqlModel>()
                        .HasIndex(b => new { b.user, b.uid })
                        .IsUnique();

            modelBuilder.Entity<SisiHistorySqlModel>()
                        .HasIndex(h => new { h.user, h.uid })
                        .IsUnique();
        }
    }

    public class SisiBookmarkSqlModel
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public string user { get; set; }

        [Required]
        public string uid { get; set; }

        public DateTime created { get; set; }

        public string json { get; set; }

        public string name { get; set; }

        public string model { get; set; }
    }

    public class SisiHistorySqlModel
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public string user { get; set; }

        [Required]
        public string uid { get; set; }

        public DateTime created { get; set; }

        public string json { get; set; }
    }
}
