using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Http;
using System.Text;
using IO = System.IO.File;
using System.IO;
using Shared;
using Shared.Engine;

namespace Merchant.Controllers
{
    /// <summary>
    /// https://pay.b2pay.io/merchant/api.php
    /// </summary>
    public class B2PAY : MerchantController
    {
        static B2PAY() { Directory.CreateDirectory("merchant/invoice/b2pay"); }


        [HttpGet]
        [Route("b2pay/new")]
        async public Task<ActionResult> Index(string email)
        {
            if (!AppInit.conf.Merchant.B2PAY.enable || string.IsNullOrWhiteSpace(email))
                return Content(string.Empty);

            Dictionary<string, dynamic> payment = new Dictionary<string, dynamic>() 
            {
                ["amount"] = AppInit.conf.Merchant.accessCost,
                ["currency"] = "USD",
                ["description"] = "Buy Premium",
                ["order_number"] = CrypTo.md5(DateTime.Now.ToBinary().ToString()),
                ["type_payment"] = "merchant",
                ["usr"] = "new",
                ["custom_field"] = decodeEmail(email),
                ["callback_url"] = CrypTo.Base64($"{AppInit.Host(HttpContext)}/b2pay/callback"),
                ["success_url"] = CrypTo.Base64($"{AppInit.Host(HttpContext)}/buy/success.html"),
                ["error_url"] = CrypTo.Base64($"{AppInit.Host(HttpContext)}/buy/error.html")
            };

            payment.Add("signature", CrypTo.Base64(CrypTo.md5binary($"{AppInit.conf.Merchant.accessCost}:{payment["callback_url"]}:{payment["currency"]}:{payment["custom_field"]}:{payment["description"]}:{payment["error_url"]}:{payment["order_number"]}:{payment["success_url"]}:merchant:new:{AppInit.conf.Merchant.B2PAY.encryption_password}")));

            string data = $"payment={CrypTo.Base64(CrypTo.AES256(JsonConvert.SerializeObject(payment), AppInit.conf.Merchant.B2PAY.encryption_password, AppInit.conf.Merchant.B2PAY.encryption_iv))}&id={AppInit.conf.Merchant.B2PAY.username_id}";

            var root = await Http.Post<JObject>(AppInit.conf.Merchant.B2PAY.sandbox ? "https://pay.b2pay.io/api_sandbox/merchantpayments.php" : "https://pay.b2pay.io/api/merchantpayments.php", data);
            if (root == null || !root.ContainsKey("data"))
                return Content("data == null");

            string invoiceurl = root.Value<JObject>("data")?.Value<string>("url");
            if (string.IsNullOrWhiteSpace(invoiceurl))
                return Content("invoiceurl == null");

            IO.WriteAllText($"merchant/invoice/b2pay/{payment["order_number"]}", JsonConvert.SerializeObject(payment));

            return Redirect(invoiceurl);
        }


        [HttpPost]
        [Route("b2pay/callback")]
        async public Task<ActionResult> Callback()
        {
            if (!AppInit.conf.Merchant.B2PAY.enable || HttpContext.Request.Method != HttpMethods.Post || HttpContext.Request.ContentLength == 0)
                return StatusCode(404);

            var buffer = new byte[Convert.ToInt32(HttpContext.Request.ContentLength)];
            await HttpContext.Request.Body.ReadAsync(buffer, 0, buffer.Length);

            var requestContent = Encoding.UTF8.GetString(buffer);
            WriteLog("b2pay", requestContent);

            JObject result = JsonConvert.DeserializeObject<JObject>(requestContent);
            string signature = CrypTo.Base64(CrypTo.md5binary($"{result.Value<string>("amount")}:{result.Value<string>("currency")}:{result.Value<string>("gatewayAmount")}:{result.Value<string>("gatewayCurrency")}:{result.Value<string>("gatewayRate")}:{result.Value<string>("orderNumber")}:{result.Value<string>("pay_id")}:{result.Value<string>("sanitizedMask")}:{result.Value<string>("status")}:{result.Value<string>("token")}:pay:{AppInit.conf.Merchant.B2PAY.encryption_password}"));

            if (result.Value<string>("sign") != signature)
                return StatusCode(401);

            string orderNumber = result.Value<string>("orderNumber");
            if (result.Value<string>("status") != "approved" || string.IsNullOrWhiteSpace(orderNumber) || !IO.Exists($"merchant/invoice/b2pay/{orderNumber}"))
                return StatusCode(403);

            var invoice = JsonConvert.DeserializeObject<Dictionary<string, string>>(IO.ReadAllText($"merchant/invoice/b2pay/{orderNumber}"));
            PayConfirm(invoice["custom_field"], "b2pay", orderNumber);

            return Content("ok");
        }
    }
}
