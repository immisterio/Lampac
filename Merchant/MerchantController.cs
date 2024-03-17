using Lampac;
using Lampac.Engine;
using System;
using System.Web;

namespace Merchant
{
    public class MerchantController : BaseController
    {
        static DateTime LastWriteTimeUsers = default;

        static string _users = null;

        public static void PayConfirm(string email, string merch, string order, int days = 0)
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
                DateTime ex = default;

                if (days > 0)
                {
                    if (AppInit.conf.accsdb.accounts.TryGetValue(email, out ex))
                    {
                        ex = ex > DateTime.UtcNow ? ex.AddDays(days) : DateTime.UtcNow.AddDays(days);
                        AppInit.conf.accsdb.accounts[email] = ex;
                    }
                    else
                    {
                        ex = DateTime.UtcNow.AddDays(days);
                        AppInit.conf.accsdb.accounts.TryAdd(email, ex);
                    }
                }
                else
                {
                    if (AppInit.conf.accsdb.accounts.TryGetValue(email, out ex))
                    {
                        ex = ex > DateTime.UtcNow ? ex.AddMonths(AppInit.conf.Merchant.accessForMonths) : DateTime.UtcNow.AddMonths(AppInit.conf.Merchant.accessForMonths);
                        AppInit.conf.accsdb.accounts[email] = ex;
                    }
                    else
                    {
                        ex = DateTime.UtcNow.AddMonths(AppInit.conf.Merchant.accessForMonths);
                        AppInit.conf.accsdb.accounts.TryAdd(email, ex);
                    }
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


        public static string decodeEmail(string email)
        {
            return HttpUtility.UrlDecode(email.ToLower().Trim());
        }
    }
}
