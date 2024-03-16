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
    }
}
