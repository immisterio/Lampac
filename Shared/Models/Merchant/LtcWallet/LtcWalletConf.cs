namespace Shared.Models.Merchant.LtcWallet
{
    public class LtcWalletConf
    {
        public bool enable { get; set; }

        public string rpc { get; set; } = "http://127.0.0.1:9332/";

        public string rpcuser { get; set; } = "ltc";

        public string rpcpassword { get; set; } = "ltc";
    }
}
