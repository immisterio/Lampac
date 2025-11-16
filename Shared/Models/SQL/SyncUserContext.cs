using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Models.SQL
{
    public static class SyncUserDb
    {
        public static void Initialization()
        {
            Directory.CreateDirectory("database");

            try
            {
                using (var sqlDb = new SyncUserContext())
                {
                    sqlDb.Database.EnsureCreated();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SyncUserDb initialization failed: {ex.Message}");
            }
        }


        public static SyncUserContext GetSyncUserContext(this HttpContext httpContext)
        {
            if (httpContext.Items[nameof(SyncUserContext)] is SyncUserContext context)
                return context;

            context = new SyncUserContext();
            httpContext.Items[nameof(SyncUserContext)] = context;

            httpContext.Response.OnCompleted(() =>
            {
                context?.Dispose();
                return Task.CompletedTask;
            });

            return context;
        }
    }

    public class SyncUserContext : DbContext
    {
        public DbSet<SyncUserTimecodeSqlModel> timecodes { get; set; }

        public DbSet<SyncUserBookmarkSqlModel> bookmarks { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=database/SyncUser.sql");
            optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
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
