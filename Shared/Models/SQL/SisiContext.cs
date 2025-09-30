using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Models.SQL
{
    public static class SisiDb
    {
        public static readonly SisiContext Read, Write;

        static SisiDb()
        {
            Directory.CreateDirectory("database");

            try
            {
                Write = new SisiContext();
                Write.ChangeTracker.AutoDetectChangesEnabled = false;
                Write.Database.EnsureCreated();

                Read = new SisiContext();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SisiDb initialization failed: {ex.Message}");
            }
        }
    }


    public class SisiContext : DbContext
    {
        public DbSet<SisiBookmarkSqlModel> bookmarks { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=database/Sisi.sql");
            optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SisiBookmarkSqlModel>()
                        .HasIndex(b => new { b.user, b.uid })
                        .IsUnique();
        }
    }

    public class SisiBookmarkSqlModel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
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
}
