namespace Shared.Models.Base
{
    public class AccsUser
    {
        public string id { get; set; }

        public List<string> ids { get; set; } = new List<string>();

        public bool IsPasswd { get; set; }

        public DateTime expires { get; set; }

        public int group { get; set; }

        public bool ban { get; set; }

        public string ban_msg { get; set; }

        public string comment { get; set; }

        public Dictionary<string, object> @params { get; set; }
    }
}
