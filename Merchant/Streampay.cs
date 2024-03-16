using Microsoft.AspNetCore.Mvc;
using System;
using Merchant;
using IO = System.IO.File;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Chaos.NaCl;
using System.Text.RegularExpressions;
using System.Linq;

namespace Lampac.Controllers.LITE
{
    public class Streampay : MerchantController
    {
        static Streampay() { Directory.CreateDirectory("merchant/invoice/streampay"); }


        [HttpGet]
        [Route("streampay/new")]
        async public Task<ActionResult> Index(string email)
        {
            var init = AppInit.conf.Merchant.Streampay;
            if (!init.enable || string.IsNullOrWhiteSpace(email))
                return Content(string.Empty);

            email = email.ToLower().Trim();
            string transid = DateTime.Now.ToBinary().ToString().Replace("-", "");
            IO.WriteAllText($"merchant/invoice/streampay/{transid}", email);

            var body = new
            {
                init.store_id,
                external_id = transid,
                description = "Донат автору на развитие проекта",
                system_currency = "USDT",
                payment_type = 2,
                amount = AppInit.conf.Merchant.accessCost
            };

            var jsonBody = JsonSerializer.Serialize(body);
            string signature = Sign(Encoding.UTF8.GetBytes(jsonBody + DateTime.UtcNow.ToString("yyyyMMdd:HHmm")), init.private_key);

            using (var httpClient = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.streampay.org/api/payment/create")
                {
                    Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                };

                request.Headers.Add("signature", signature);

                var response = await httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    string respData = await response.Content.ReadAsStringAsync();
                    string pay_url = Regex.Match(respData, "\"pay_url\":\"([^\"]+)\"").Groups[1].Value;

                    if (!string.IsNullOrEmpty(pay_url))
                        return Redirect(pay_url);

                    return Content(respData);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return Content("Invalid signature");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotAcceptable)
                {
                    return Content("Invalid request data");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                {
                    return Content("Internal server error");
                }
            }

            return Content("error");
        }


        [HttpGet]
        [Route("streampay/callback")]
        public ActionResult Callback()
        {
            if (!AppInit.conf.Merchant.Streampay.enable || !IO.Exists($"merchant/invoice/streampay/{Request.Query["external_id"]}"))
                return StatusCode(403);

            var now = DateTime.UtcNow;
            var queryParams = Request.Query.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}").ToList();
            string paramsStr = string.Join('&', queryParams);
            byte[] paramsBuf = Encoding.UTF8.GetBytes(paramsStr);

            IO.AppendAllText("merchant/log/streampay.txt", paramsStr + "\n\n\n");

            for (int i = 0; i < 2; i++)
            {
                var tm = now.ToString("yyyyMMdd:HHmm");
                var bufToSign = paramsBuf.Concat(Encoding.UTF8.GetBytes(tm)).ToArray();

                if (Verify(Request.Headers["signature"], bufToSign, AppInit.conf.Merchant.Streampay.public_key))
                {
                    string email = IO.ReadAllText($"merchant/invoice/streampay/{Request.Query["external_id"]}");
                    PayConfirm(email, "streampay", Request.Query["external_id"]);

                    return Ok();
                }

                now = now.AddMinutes(-1);
            }

            return Forbid();
        }



        static string Sign(byte[] message, string privateKey)
        {
            var bytes = Ed25519.Sign(message, HexToBytes(privateKey));

            StringBuilder hex = new StringBuilder(bytes.Length * 2);

            foreach (byte b in bytes)
                hex.AppendFormat("{0:x2}", b);

            return hex.ToString();
        }

        static bool Verify(string signature, byte[] message, string publicKey)
        {
            return Ed25519.Verify(Encoding.UTF8.GetBytes(signature), message, HexToBytes(publicKey));
        }

        static byte[] HexToBytes(string key)
        {
            if (key.Length % 2 != 0)
                return null;

            int byteCount = key.Length / 2;
            byte[] privateKey = new byte[byteCount];

            for (int i = 0; i < byteCount; i++)
            {
                string byteString = key.Substring(i * 2, 2);
                byte keyValue = Convert.ToByte(byteString, 16);
                privateKey[i] = keyValue;
            }

            return privateKey;
        }
    }
}
