using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;

namespace Shared.Models.SQL
{
    public class SyncUserContext : DbContext
    {
        public static void Configure()
        {
            try
            {
                Directory.CreateDirectory("database");

                using (var context = new SyncUserContext())
                    context.Database.EnsureCreated();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SyncUser.sql initialization failed: {ex.Message}");
            }
        }

        public DbSet<SyncUserTimecodeSqlModel> timecodes { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (Startup.IsShutdown)
                return;

            optionsBuilder.UseSqlite("Data Source=database/SyncUser.sql");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SyncUserTimecodeSqlModel>()
                        .HasIndex(t => new { t.user, t.card, t.item })
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

        [Required]
        public string data { get; set; }

        public DateTime updated { get; set; }
    }
}
