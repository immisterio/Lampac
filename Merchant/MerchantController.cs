using Lampac;
using Lampac.Engine;
using System;

namespace Merchant
{
    public class MerchantController : BaseController
    {
        public static void PayConfirm(string email, string merch, string order)
        {
            string users = System.IO.File.ReadAllText("merchant/users.txt");

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
            }
        }
    }
}
