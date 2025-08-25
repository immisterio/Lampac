using Shared.Models.Merchant.LtcWallet;

namespace Shared.Models.Merchant
{
    public class MerchantsModel
    {
        public int accessCost { get; set; } = 2;

        public int accessForMonths { get; set; } = 1;

        public int allowedDifference { get; set; }

        public int defaultGroup { get; set; }

        public B2payConf B2PAY { get; set; } = new B2payConf();

        public CryptoCloudConf CryptoCloud { get; set; } = new CryptoCloudConf();

        public FreekassaConf FreeKassa { get; set; } = new FreekassaConf();

        public StreampayConf Streampay { get; set; } = new StreampayConf();

        public LtcWalletConf LtcWallet { get; set; } = new LtcWalletConf();
    }
}
