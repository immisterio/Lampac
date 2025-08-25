using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using IO = System.IO.File;
using System.IO;
using Microsoft.Extensions.Caching.Memory;
using System;
using Shared;
using Shared.Engine;
using Shared.Models;

namespace Merchant.Controllers
{
    /// <summary>
    /// https://app.cryptocloud.plus/integration/api
    /// </summary>
    public class CryptoCloud : MerchantController
    {
        static CryptoCloud() { Directory.CreateDirectory("merchant/invoice/cryptocloud"); }


        [HttpGet]
        [Route("cryptocloud/invoice/create")]
        async public Task<ActionResult> Index(string email)
        {
            if (!AppInit.conf.Merchant.CryptoCloud.enable || string.IsNullOrWhiteSpace(email))
                return Content(string.Empty);

            email = decodeEmail(email);

            Dictionary<string, string> postParams = new Dictionary<string, string>()
            {
                ["amount"] = AppInit.conf.Merchant.accessCost.ToString(),
                ["shop_id"] = AppInit.conf.Merchant.CryptoCloud.SHOPID,
                //["currency"] = "USD",
                //["order_id"] = CrypTo.md5(DateTime.Now.ToBinary().ToString()),
                ["email"] = email
            };

            if (memoryCache.TryGetValue($"cryptocloud:{email}", out string pay_url))
                return Redirect(pay_url);

            var root = await Http.Post<JObject>("https://api.cryptocloud.plus/v1/invoice/create", new System.Net.Http.FormUrlEncodedContent(postParams), headers: HeadersModel.Init("Authorization", $"Token {AppInit.conf.Merchant.CryptoCloud.APIKEY}"));
            if (root == null || !root.ContainsKey("pay_url"))
                return Content("root == null");

            pay_url = root.Value<string>("pay_url");
            if (string.IsNullOrWhiteSpace(pay_url))
                return Content("pay_url == null");

            memoryCache.Set($"cryptocloud:{email}", pay_url, DateTime.Now.AddHours(2));

            IO.WriteAllText($"merchant/invoice/cryptocloud/{root.Value<string>("invoice_id")}", JsonConvert.SerializeObject(postParams));

            return Redirect(pay_url);
        }


        [HttpPost]
        [Route("cryptocloud/callback")]
        async public Task<ActionResult> Callback(string invoice_id)
        {
            if (!AppInit.conf.Merchant.CryptoCloud.enable || !IO.Exists($"merchant/invoice/cryptocloud/{invoice_id}"))
                return StatusCode(403);

            WriteLog("cryptocloud", JsonConvert.SerializeObject(HttpContext.Request.Form));

            var root = await Http.Get<JObject>("https://api.cryptocloud.plus/v1/invoice/info?uuid=INV-" + invoice_id, headers: HeadersModel.Init("Authorization", $"Token {AppInit.conf.Merchant.CryptoCloud.APIKEY}"));
            if (root == null || root.Value<string>("status") != "success")
                return StatusCode(403);

            if (root.Value<string>("status_invoice") is "paid" or "overpaid")
            {
                var invoice = JsonConvert.DeserializeObject<Dictionary<string, string>>(IO.ReadAllText($"merchant/invoice/cryptocloud/{invoice_id}"));
                PayConfirm(invoice["email"], "cryptocloud", invoice_id);

                return StatusCode(200);
            }

            return StatusCode(403);
        }
    }
}
