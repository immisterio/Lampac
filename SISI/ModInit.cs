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

            cleanupTimer = new Timer(_ => CleanupHistory(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(20));
        }

        static int _updatingDb = 0;
        private static void CleanupHistory()
        {
            if (Interlocked.Exchange(ref _updatingDb, 1) == 1)
                return;

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
            finally
            {
                Volatile.Write(ref _updatingDb, 0);
            }
        }
    }
}
