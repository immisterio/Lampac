﻿namespace Shared.Models.Module
{
    public class SisiEventsModel
    {
        public SisiEventsModel(string rchtype, string account_email, string uid, string token)
        {
            this.rchtype = rchtype;
            this.account_email = account_email;
            this.uid = uid;
            this.token = token;
        }

        public string rchtype { get; set; }
        public string account_email { get; set; }
        public string uid { get; set; }
        public string token { get; set; }
    }
}
