namespace Shared.Models.Merchant
{
    public class StreampayConf
    {
        public bool enable { get; set; }

        public long store_id { get; set; }

        public string public_key { get; set; }

        public string private_key { get; set; }
    }
}
