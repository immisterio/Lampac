using Jackett;
using JacRed.Models.AppConf;
using Lampac.Engine;
using Lampac.Models.AppConf;

namespace JacRed.Engine
{
    public class JacBaseController : BaseController
    {
        public static RedConf red => ModInit.conf.Red;

        public static JacConf jackett => ModInit.conf.Jackett;
    }
}
