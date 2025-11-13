using Microsoft.EntityFrameworkCore;
using Shared.Models.SQL;
using System;
using System.Linq;
using System.Threading;

namespace SISI
{
    public class ModInit
    {
        private static Timer cleanupTimer;

        public static void loaded()
        {
            Directory.CreateDirectory("wwwroot/bookmarks/img");
            Directory.CreateDirectory("wwwroot/bookmarks/preview");

            cleanupTimer ??= new Timer(_ => CleanupHistory(), null, TimeSpan.Zero, TimeSpan.FromHours(1));
        }

        private static void CleanupHistory()
        {
            try
            {
                var threshold = DateTime.UtcNow.AddDays(-30);
                var sqlDb = SisiDb.Write;

                sqlDb?.historys
                    .Where(i => i.created < threshold)
                    .ExecuteDelete();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SISI] Cleanup history failed: {ex.Message}");
            }
        }
    }
}
