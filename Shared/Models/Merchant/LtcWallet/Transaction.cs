namespace Shared.Models.Merchant.LtcWallet
{
    public class Transaction
    {
        public string address { get; set; }

        public string category { get; set; }

        public double amount { get; set; }

        public string status { get; set; }

        public string txid { get; set; }
    }
}
