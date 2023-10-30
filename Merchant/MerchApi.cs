using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;

namespace Lampac.Controllers.LITE
{
    public class MerchApi : BaseController
    {
        [HttpGet]
        [Route("merchant/user")]
        public ActionResult Index(string account_email)
        {
            return Json(AppInit.conf.accsdb.accounts[account_email]);
        }
    }
}
