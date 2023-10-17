using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;

namespace Lampac.Controllers.LITE
{
    public class MerchApi : BaseController
    {
        [HttpGet]
        [Route("merchant/user")]
        public ActionResult Index(string email)
        {
            email = email?.ToLower()?.Trim();
            if (email != null && AppInit.conf.accsdb.accounts.ContainsKey(email))
                return Json(AppInit.conf.accsdb.accounts[email]);

            return Content(string.Empty);
        }
    }
}
