using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Text.RegularExpressions;
using Lampac.Models.Merchant.LtcWallet;
using Shared;
using System.Threading;

namespace Lampac.Controllers.LITE
{
    public class Litecoin : BaseController
    {
        #region Litecoin
        static Litecoin()
        {
            ThreadPool.QueueUserWorkItem(async _ => await ChekTransactions());
        }
        #endregion

        #region LtcKurs
        async static ValueTask<double> LtcKurs()
        {
            if (!Startup.memoryCache.TryGetValue("Litecoin:kurs:ltc", out double kurs))
            {
                var exmo = await HttpClient.Get<JObject>("https://api.exmo.com/v1.1/ticker");
                var LTC_USD = exmo.GetValue("LTC_USD");

                double avg = LTC_USD.Value<double>("avg");
                double buy_price = LTC_USD.Value<double>("buy_price");

                kurs = avg > buy_price ? buy_price : avg;
                Startup.memoryCache.Set("Litecoin:kurs:ltc", kurs, DateTime.Now.AddMinutes(15));
            }

            return kurs;
        }
        #endregion

        [HttpGet]
        [Route("litecoin/getnewaddress")]
        async public Task<ActionResult> Index(string email)
        {
            if (!AppInit.conf.Merchant.LtcWallet.enable || string.IsNullOrWhiteSpace(email))
                return Content(string.Empty);

            string pathEmail = $"merchant/invoice/litecoin/{CrypTo.md5(email.ToLower().Trim())}.email";
            double buyprice = await LtcKurs();

            if (System.IO.File.Exists(pathEmail))
            {
                return Json(new
                {
                    payinaddress = System.IO.File.ReadAllText(pathEmail),
                    buyprice,
                    amount = AppInit.conf.Merchant.accessCost / buyprice
                });
            }
            else
            {
                string json = await HttpClient.Post(AppInit.conf.Merchant.LtcWallet.rpc, "{\"method\": \"getnewaddress\"}", addHeaders: new List<(string name, string val)>()
                {
                    ("Authorization", $"Basic {CrypTo.Base64($"{AppInit.conf.Merchant.LtcWallet.rpcuser}:{AppInit.conf.Merchant.LtcWallet.rpcpassword}")}")
                });

                string payinAddress = Regex.Match(json ?? string.Empty, "\"result\":\"([^\"]+)\"").Groups[1].Value.Trim();

                if (string.IsNullOrWhiteSpace(payinAddress) || !Regex.IsMatch(payinAddress, "^[0-9a-zA-Z]+$") || 20 > payinAddress.Length)
                {
                    return Json(new { });
                }
                else
                {
                    System.IO.File.WriteAllText(pathEmail, payinAddress);
                    System.IO.File.WriteAllText($"merchant/invoice/litecoin/{payinAddress}.ltc", email.ToLower().Trim());
                }

                return Json(new
                {
                    payinaddress = payinAddress,
                    buyprice,
                    amount = AppInit.conf.Merchant.accessCost / buyprice
                });
            }
        }


        #region ChekTransactions
        async static Task ChekTransactions()
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(1));
                if (!AppInit.conf.Merchant.LtcWallet.enable)
                    continue;

                try
                {
                    double kurs = await LtcKurs();
                    if (kurs == -1)
                        continue;

                    var root = await HttpClient.Post<RootTransactions>(AppInit.conf.Merchant.LtcWallet.rpc, "{\"method\": \"listtransactions\", \"params\": [\"*\", 20]}", addHeaders: new List<(string name, string val)>()
                    {
                        ("Authorization", $"Basic {CrypTo.Base64($"{AppInit.conf.Merchant.LtcWallet.rpcuser}:{AppInit.conf.Merchant.LtcWallet.rpcpassword}")}")
                    });

                    var transactions = root?.result;
                    if (transactions == null || transactions.Count == 0)
                        continue;

                    foreach (var trans in transactions)
                    {
                        if (trans.category != "receive" || string.IsNullOrWhiteSpace(trans.txid))
                            continue;

                        try
                        {
                            if (System.IO.File.Exists($"merchant/invoice/litecoin/{trans.txid}.txid"))
                                continue;

                            string email = System.IO.File.ReadAllText($"merchant/invoice/litecoin/{trans.address}.ltc");
                            System.IO.File.WriteAllText($"merchant/invoice/litecoin/{trans.txid}.txid", $"{email}\n{trans.address}");

                            double cost = (double)AppInit.conf.Merchant.accessCost / (double)(AppInit.conf.Merchant.accessForMonths * 30);
                            int addday = (int)((trans.amount * kurs) / cost);

                            await System.IO.File.AppendAllTextAsync("merchant/users.txt", $"{email},{DateTime.UtcNow.AddDays(addday).ToFileTimeUtc()},litecoin\n");

                            AppInit.conf.accsdb.accounts.Add(email);
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
        #endregion
    }
}
