using Lampac;
using Lampac.Engine;
using System;

namespace Merchant
{
    public class MerchantController : BaseController
    {
        static DateTime LastWriteTimeUsers = default;

        static string _users = null;

        public static void PayConfirm(string email, string merch, string order)
        {
            var lastWriteTimeUsers = System.IO.File.GetLastWriteTime("merchant/users.txt");

            if (_users == null || LastWriteTimeUsers != lastWriteTimeUsers)
            {
                LastWriteTimeUsers = lastWriteTimeUsers;
                _users = System.IO.File.ReadAllText("merchant/users.txt");
            }

            string users = _users;

            if (!users.Contains($",{merch},{order}"))
            {
                if (AppInit.conf.accsdb.accounts.TryGetValue(email, out DateTime ex))
                {
                    ex = ex > DateTime.UtcNow ? ex.AddMonths(AppInit.conf.Merchant.accessForMonths) : DateTime.UtcNow.AddMonths(AppInit.conf.Merchant.accessForMonths);
                    AppInit.conf.accsdb.accounts[email] = ex;
                }
                else
                {
                    ex = DateTime.UtcNow.AddMonths(AppInit.conf.Merchant.accessForMonths);
                    AppInit.conf.accsdb.accounts.TryAdd(email, ex);
                }

                System.IO.File.AppendAllText("merchant/users.txt", $"{email.ToLower()},{ex.ToFileTimeUtc()},{merch},{order}\n");

                _users += $"{email.ToLower()},{ex.ToFileTimeUtc()},{merch},{order}\n";
                LastWriteTimeUsers = System.IO.File.GetLastWriteTime("merchant/users.txt");
            }
        }


        public static void WriteLog(string merch, string content)
        {
            try
            {
                System.IO.File.AppendAllText($"merchant/log/{merch}.txt", content + "\n\n\n");
            }
            catch { }
        }
    }
}
