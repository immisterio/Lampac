using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using System;
using Newtonsoft.Json;

namespace Lampac.Controllers.LITE
{
    /// <summary>
    /// https://docs.freekassa.ru/
    /// </summary>
    public class FreeKassa : BaseController
    {
        [HttpGet]
        [Route("freekassa/new")]
        public ActionResult Index(string email)
        {
            if (!AppInit.conf.Merchant.FreeKassa.enable || string.IsNullOrWhiteSpace(email))
                return Content(string.Empty);

            string transid = DateTime.Now.ToBinary().ToString().Replace("-", "");

            System.IO.File.WriteAllText($"merchant/invoice/freekassa/{transid}", email.ToLower().Trim());

            string hash = CrypTo.md5($"{AppInit.conf.Merchant.FreeKassa.shop_id}:{AppInit.conf.Merchant.accessCost}:{AppInit.conf.Merchant.FreeKassa.secret}:USD:{transid}");
            return Redirect("https://pay.freekassa.ru/" + $"?m={AppInit.conf.Merchant.FreeKassa.shop_id}&oa={AppInit.conf.Merchant.accessCost}&o={transid}&s={hash}&currency=USD");
        }


        [HttpPost]
        [Route("freekassa/callback")]
        public ActionResult Callback(string AMOUNT, long MERCHANT_ORDER_ID, string SIGN)
        {
            if (!AppInit.conf.Merchant.FreeKassa.enable || !System.IO.File.Exists($"merchant/invoice/freekassa/{MERCHANT_ORDER_ID}"))
                return StatusCode(403);

            System.IO.File.AppendAllText("merchant/log/freekassa.txt", JsonConvert.SerializeObject(HttpContext.Request.Form) + "\n\n\n");

            if (CrypTo.md5($"{AppInit.conf.Merchant.FreeKassa.shop_id}:{AMOUNT}:{AppInit.conf.Merchant.FreeKassa.secret}:{MERCHANT_ORDER_ID}") == SIGN)
            {
                string users = System.IO.File.ReadAllText("merchant/users.txt");

                if (!users.Contains($",freekassa,{MERCHANT_ORDER_ID}"))
                {
                    string email = System.IO.File.ReadAllText($"merchant/invoice/freekassa/{MERCHANT_ORDER_ID}");

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

                    System.IO.File.AppendAllText("merchant/users.txt", $"{email.ToLower()},{ex.ToFileTimeUtc()},freekassa,{MERCHANT_ORDER_ID}\n"); 
                }

                return Content("YES");
            }

            return Content("SIGN != hash");
        }
    }
}
