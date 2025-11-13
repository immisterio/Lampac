using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

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

                #region migrate historys table
                try
                {
                    using (var conn = Write.Database.GetDbConnection())
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='historys';";
                            var res = cmd.ExecuteScalar();
                            if (res == null)
                            {
                                cmd.CommandText = @"CREATE TABLE IF NOT EXISTS historys (
                                                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                                        user TEXT NOT NULL,
                                                        uid TEXT NOT NULL,
                                                        created TEXT,
                                                        json TEXT
                                                    );";
                                cmd.ExecuteNonQuery();

                                cmd.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS IX_historys_user_uid ON historys(user, uid);";
                                cmd.ExecuteNonQuery();
                            }
                        }
                        conn.Close();
                    }
                }
                catch { }
                #endregion

                Read = new SisiContext();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SisiDb initialization failed: {ex.Message}");
            }
        }

        public static void Initialization() { }

        public static void FullDispose()
        {
            Read?.Dispose();
            Write?.Dispose();
        }
    }


    public class SisiContext : DbContext
    {
        public DbSet<SisiBookmarkSqlModel> bookmarks { get; set; }

        public DbSet<SisiHistorySqlModel> historys { get; set; }

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
