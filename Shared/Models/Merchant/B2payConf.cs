namespace Shared.Models.Merchant
{
    public class B2payConf
    {
        public bool enable { get; set; }

        public bool sandbox { get; set; }

        public long username_id { get; set; }

        public string encryption_iv { get; set; }

        public string encryption_password { get; set; }
    }
}
