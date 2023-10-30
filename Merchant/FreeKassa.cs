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
        async public Task<ActionResult> Index(string email)
        {
            if (!AppInit.conf.Merchant.FreeKassa.enable || string.IsNullOrWhiteSpace(email))
                return Content(string.Empty);

            string transid = DateTime.Now.ToBinary().ToString().Replace("-", "");

            await System.IO.File.WriteAllTextAsync($"merchant/invoice/freekassa/{transid}", email.ToLower().Trim());

            string hash = CrypTo.md5($"{AppInit.conf.Merchant.FreeKassa.shop_id}:{AppInit.conf.Merchant.accessCost}:{AppInit.conf.Merchant.FreeKassa.secret}:USD:{transid}");
            return Redirect("https://pay.freekassa.ru/" + $"?m={AppInit.conf.Merchant.FreeKassa.shop_id}&oa={AppInit.conf.Merchant.accessCost}&o={transid}&s={hash}&currency=USD");
        }


        [HttpPost]
        [Route("freekassa/callback")]
        async public Task<ActionResult> Callback(string AMOUNT, long MERCHANT_ORDER_ID, string SIGN)
        {
            if (!AppInit.conf.Merchant.FreeKassa.enable || !System.IO.File.Exists($"merchant/invoice/freekassa/{MERCHANT_ORDER_ID}"))
                return StatusCode(403);

            await System.IO.File.AppendAllTextAsync("merchant/log/freekassa.txt", JsonConvert.SerializeObject(HttpContext.Request.Form) + "\n\n\n");

            if (CrypTo.md5($"{AppInit.conf.Merchant.FreeKassa.shop_id}:{AMOUNT}:{AppInit.conf.Merchant.FreeKassa.secret}:{MERCHANT_ORDER_ID}") == SIGN)
            {
                string users = await System.IO.File.ReadAllTextAsync("merchant/users.txt");

                if (!users.Contains($",freekassa,{MERCHANT_ORDER_ID}"))
                {
                    string email = await System.IO.File.ReadAllTextAsync($"merchant/invoice/freekassa/{MERCHANT_ORDER_ID}");

                    if (AppInit.conf.accsdb.accounts.TryGetValue(email, out DateTime ex))
                    {
                        ex = ex > DateTime.UtcNow ? ex.AddMonths(AppInit.conf.Merchant.accessForMonths) : ex;
                        AppInit.conf.accsdb.accounts[email] = ex;
                    }
                    else
                    {
                        ex = DateTime.UtcNow.AddMonths(AppInit.conf.Merchant.accessForMonths);
                        AppInit.conf.accsdb.accounts.TryAdd(email, ex);
                    }

                    await System.IO.File.AppendAllTextAsync("merchant/users.txt", $"{email.ToLower()},{ex.ToFileTimeUtc()},freekassa,{MERCHANT_ORDER_ID}\n"); 
                }

                return Content("YES");
            }

            return Content("SIGN != hash");
        }
    }
}
