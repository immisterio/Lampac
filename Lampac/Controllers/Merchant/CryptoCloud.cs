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
                ["email"] = email
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

            var invoice = JsonConvert.DeserializeObject<Dictionary<string, string>>(await System.IO.File.ReadAllTextAsync($"merchant/invoice/cryptocloud/{invoice_id}"));
            await System.IO.File.AppendAllTextAsync("merchant/users.txt", $"{invoice["email"].ToLower()},{DateTime.UtcNow.AddYears(1).ToFileTimeUtc()},cryptocloud\n");

            if (!AppInit.conf.accsdb.accounts.Contains(invoice["email"].ToLower())) 
                AppInit.cacheconf.Item2 = default;

            return StatusCode(200);
        }
    }
}
