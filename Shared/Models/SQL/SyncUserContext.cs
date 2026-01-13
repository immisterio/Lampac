using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading;

namespace Shared.Models.SQL
{
    public partial class SyncUserContext
    {
        public static readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        public static IDbContextFactory<SyncUserContext> Factory { get; set; }

        public static void Initialization() 
        {
            Directory.CreateDirectory("database");

            try
            {
                using (var sqlDb = new SyncUserContext())
                    sqlDb.Database.EnsureCreated();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SyncUserDb initialization failed: {ex.Message}");
            }
        }

        public static void ConfiguringDbBuilder(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite(new SqliteConnectionStringBuilder
                {
                    DataSource = "database/SyncUser.sql",
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


    public partial class SyncUserContext : DbContext
    {
        public DbSet<SyncUserTimecodeSqlModel> timecodes { get; set; }

        public DbSet<SyncUserBookmarkSqlModel> bookmarks { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            ConfiguringDbBuilder(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SyncUserTimecodeSqlModel>()
                        .HasIndex(t => new { t.user, t.card, t.item })
                        .IsUnique();

            modelBuilder.Entity<SyncUserBookmarkSqlModel>()
                        .HasIndex(t => t.user)
                        .IsUnique();
        }
    }

    public class SyncUserTimecodeSqlModel
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public string user { get; set; }

        [Required]
        public string card { get; set; }

        [Required]
        public string item { get; set; }

        public string data { get; set; }

        public DateTime updated { get; set; }
    }

    [Table("bookmarks")]
    public class SyncUserBookmarkSqlModel
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public string user { get; set; }

        [Required]
        public string data { get; set; }

        public DateTime updated { get; set; }
    }
}
