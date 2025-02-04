using Shared.Model.Base;
using System.Collections.Generic;

namespace Shared.Models
{
    public class RequestModel
    {
        public bool IsLocalRequest { get; set; }

        public string IP { get; set; }

        public string UserAgent { get; set; }

        public string Country { get; set; }

        public AccsUser user { get; set; }

        public string user_uid { get; set; }

        public Dictionary<string, object> @params { get; set; }
    }
}
