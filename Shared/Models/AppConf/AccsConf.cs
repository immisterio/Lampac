using Microsoft.AspNetCore.Http;
using Shared.Models.Base;
using System.Collections.Concurrent;

namespace Shared.Models.AppConf
{
    public class AccsConf
    {
        public bool enable { get; set; }

        public string shared_passwd { get; set; }

        public int shared_daytime { get; set; }

        public string whitepattern { get; set; }

        public HashSet<string> white_uids { get; set; }

        public string premium_pattern { get; set; }

        public string domainId_pattern { get; set; }

        public int maxip_hour { get; set; }

        public int maxrequest_hour { get; set; }

        public int maxlock_day { get; set; }

        public int blocked_hour { get; set; }

        public string authMesage { get; set; }

        public string denyMesage { get; set; }

        public string denyGroupMesage { get; set; }

        public string expiresMesage { get; set; }

        public Dictionary<string, object> @params { get; set; }

        public Dictionary<string, DateTime> accounts { get; set; } = new Dictionary<string, DateTime>();

        public ConcurrentBag<AccsUser> users { get; set; } = new ConcurrentBag<AccsUser>();


        public AccsUser findUser(HttpContext httpContext, out string uid)
        {
            var user = findUser(httpContext.Request.Query["token"].ToString()) ??
                       findUser(httpContext.Request.Query["account_email"].ToString()) ??
                       findUser(httpContext.Request.Query["uid"].ToString()) ??
                       findUser(httpContext.Request.Query["box_mac"].ToString());

            if (user != null)
            {
                uid = user.id;
                return user;
            }

            uid = null;
            return null;
        }

        public AccsUser findUser(string uid)
        {
            if (string.IsNullOrEmpty(uid))
                return null;

            uid = uid.ToLower().Trim();
            return users.FirstOrDefault(i => (i.id != null && i.id.ToLower() == uid) || (i.ids != null && i.ids.FirstOrDefault(id => id.ToLower() == uid) != null));
        }
    }
}
