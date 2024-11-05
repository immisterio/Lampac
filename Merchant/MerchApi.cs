using Microsoft.AspNetCore.Mvc;
using Merchant;

namespace Lampac.Controllers.LITE
{
    public class MerchApi : MerchantController
    {
        [HttpGet]
        [Route("merchant/user")]
        public ActionResult Index(string account_email)
        {
            return Json(AppInit.conf.accsdb.accounts[decodeEmail(account_email)]);
        }


        [Route("merchant/payconfirm")]
        public ActionResult ConfirmPay(string passwd, string account_email, string merch, string order, int days = 0)
        {
            if (passwd != System.IO.File.ReadAllText("passwd"))
                return Content("incorrect passwd");

            string email = decodeEmail(account_email);
            PayConfirm(email, merch, order, days);

            return Json(AppInit.conf.accsdb.accounts[email]);
        }
    }
}
