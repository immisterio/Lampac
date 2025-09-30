using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Models.SQL
{
    public static class SyncUserDb
    {
        public static readonly SyncUserContext Read, Write;

        static SyncUserDb()
        {
            Directory.CreateDirectory("database");

            try
            {
                Write = new SyncUserContext();
                Write.ChangeTracker.AutoDetectChangesEnabled = false;
                Write.Database.EnsureCreated();

                Read = new SyncUserContext();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SyncUserDb initialization failed: {ex.Message}");
            }
        }

        public static void FullDispose()
        {
            Read?.Dispose();
            Write?.Dispose();
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
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
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
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        public string user { get; set; }

        [Required]
        public string data { get; set; }

        public DateTime updated { get; set; }
    }
}
