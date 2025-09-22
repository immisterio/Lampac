using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Models.SQL
{
    public class SisiContext : DbContext
    {
        public static void Configure()
        {
            try
            {
                Directory.CreateDirectory("database");

                using (var context = new SisiContext())
                    context.Database.EnsureCreated();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Sisi.sql initialization failed: {ex.Message}");
            }
        }

        public DbSet<SisiBookmarkSqlModel> bookmarks { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (Startup.IsShutdown)
                return;

            optionsBuilder.UseSqlite("Data Source=database/Sisi.sql");
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

        [Required]
        public string json { get; set; }
    }
}
