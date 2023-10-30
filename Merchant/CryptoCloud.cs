using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;

namespace Lampac.Controllers.LITE
{
    /// <summary>
    /// https://app.cryptocloud.plus/integration/api
    /// </summary>
    public class CryptoCloud : BaseController
    {
        [HttpGet]
        [Route("cryptocloud/invoice/create")]
        async public Task<ActionResult> Index(string email)
        {
            if (!AppInit.conf.Merchant.CryptoCloud.enable || string.IsNullOrWhiteSpace(email))
                return Content(string.Empty);

            Dictionary<string, string> postParams = new Dictionary<string, string>()
            {
                ["amount"] = AppInit.conf.Merchant.accessCost.ToString(),
                ["shop_id"] = AppInit.conf.Merchant.CryptoCloud.SHOPID,
                //["currency"] = "USD",
                //["order_id"] = CrypTo.md5(DateTime.Now.ToBinary().ToString()),
                ["email"] = email.ToLower().Trim()
            };

            var root = await HttpClient.Post<JObject>("https://api.cryptocloud.plus/v1/invoice/create", new System.Net.Http.FormUrlEncodedContent(postParams), addHeaders: new List<(string name, string val)>()
            {
                ("Authorization", $"Token {AppInit.conf.Merchant.CryptoCloud.APIKEY}")
            });

            if (root == null || !root.ContainsKey("pay_url"))
                return Content("root == null");

            string pay_url = root.Value<string>("pay_url");
            if (string.IsNullOrWhiteSpace(pay_url))
                return Content("pay_url == null");

            await System.IO.File.WriteAllTextAsync($"merchant/invoice/cryptocloud/{root.Value<string>("invoice_id")}", JsonConvert.SerializeObject(postParams));

            return Redirect(pay_url);
        }


        [HttpPost]
        [Route("cryptocloud/callback")]
        async public Task<ActionResult> Callback(string invoice_id)
        {
            if (!AppInit.conf.Merchant.CryptoCloud.enable || !System.IO.File.Exists($"merchant/invoice/cryptocloud/{invoice_id}"))
                return StatusCode(403);

            await System.IO.File.AppendAllTextAsync("merchant/log/cryptocloud.txt", JsonConvert.SerializeObject(HttpContext.Request.Form) + "\n\n\n");

            var root = await HttpClient.Get<JObject>("https://api.cryptocloud.plus/v1/invoice/info?uuid=INV-" + invoice_id, addHeaders: new List<(string name, string val)>()
            {
                ("Authorization", $"Token {AppInit.conf.Merchant.CryptoCloud.APIKEY}")
            });

            if (root == null || root.Value<string>("status") != "success" || root.Value<string>("status_invoice") != "paid")
                return StatusCode(403);

            string users = await System.IO.File.ReadAllTextAsync("merchant/users.txt");

            if (!users.Contains($",cryptocloud,{invoice_id}"))
            {
                var invoice = JsonConvert.DeserializeObject<Dictionary<string, string>>(await System.IO.File.ReadAllTextAsync($"merchant/invoice/cryptocloud/{invoice_id}"));

                if (AppInit.conf.accsdb.accounts.TryGetValue(invoice["email"], out DateTime ex))
                {
                    ex = ex > DateTime.UtcNow ? ex.AddMonths(AppInit.conf.Merchant.accessForMonths) : ex;
                    AppInit.conf.accsdb.accounts[invoice["email"]] = ex;
                }
                else
                {
                    ex = DateTime.UtcNow.AddMonths(AppInit.conf.Merchant.accessForMonths);
                    AppInit.conf.accsdb.accounts.TryAdd(invoice["email"], ex);
                }

                await System.IO.File.AppendAllTextAsync("merchant/users.txt", $"{invoice["email"]},{ex.ToFileTimeUtc()},cryptocloud,{invoice_id}\n");
            }

            return StatusCode(200);
        }
    }
}
