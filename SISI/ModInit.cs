using Microsoft.EntityFrameworkCore;
using Shared.Models.SQL;
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

            cleanupTimer = new Timer(_ => CleanupHistory(), null, TimeSpan.FromMinutes(20), TimeSpan.FromHours(1));
        }

        private static void CleanupHistory()
        {
            try
            {
                var threshold = DateTime.UtcNow.AddDays(-AppInit.conf.sisi.history.days);

                using (var sqlDb = new SisiContext())
                {
                    sqlDb.historys
                        .AsNoTracking()
                        .Where(i => i.created < threshold)
                        .ExecuteDelete();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SISI] Cleanup history failed: {ex.Message}");
            }
        }
    }
}
