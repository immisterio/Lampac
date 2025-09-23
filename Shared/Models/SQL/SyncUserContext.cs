using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
                {
                    context.Database.EnsureCreated();

                    try
                    {
                        context.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ""bookmarks"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_bookmarks"" PRIMARY KEY AUTOINCREMENT,
    ""user"" TEXT NOT NULL,
    ""data"" TEXT NOT NULL,
    ""updated"" TEXT NOT NULL
);");

                        context.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_bookmarks_user\" ON \"bookmarks\" (\"user\")");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"SyncUser.sql bookmarks initialization failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SyncUser.sql initialization failed: {ex.Message}");
            }
        }

        public DbSet<SyncUserTimecodeSqlModel> timecodes { get; set; }

        public DbSet<SyncUserBookmarkSqlModel> bookmarks { get; set; }

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
