using Microsoft.AspNetCore.Http;
using Shared.Model.Base;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lampac.Models.AppConf
{
    public class AccsConf
    {
        public bool enable { get; set; }

        public string whitepattern { get; set; }

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

        public Dictionary<string, DateTime> accounts { get; set; } = new Dictionary<string, DateTime>();

        public List<AccsUser> users { get; set; } = new List<AccsUser>();


        public AccsUser findUser(HttpContext httpContext, out string uid)
        {
            var user = findUser(httpContext.Request.Query["token"].ToString());
            if (user != null)
            {
                uid = httpContext.Request.Query["token"].ToString();
                return user;
            }

            user = findUser(httpContext.Request.Query["account_email"].ToString());
            if (user != null)
            {
                uid = httpContext.Request.Query["account_email"].ToString();
                return user;
            }

            user = findUser(httpContext.Request.Query["uid"].ToString());
            if (user != null)
            {
                uid = httpContext.Request.Query["uid"].ToString();
                return user;
            }

            user = findUser(httpContext.Request.Query["box_mac"].ToString());
            if (user != null)
            {
                uid = httpContext.Request.Query["box_mac"].ToString();
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
            return users.FirstOrDefault(i => i.id == uid || (i.ids != null && i.ids.Contains(uid)));
        }
    }
}
