﻿using Shared.Engine.CORE;
using Shared.Model.Base;
using System.Collections.Generic;

namespace Shared.Models
{
    public class RequestModel
    {
        public bool IsLocalRequest { get; set; }

        public string IP { get; set; }

        public string Path { get; set; }

        public string Query { get; set; }

        public string UserAgent { get; set; }

        #region Country
        private string _countryCode = null;
        public string Country
        {
            get
            {
                if (_countryCode == string.Empty)
                    return null;

                if (_countryCode != null)
                    return _countryCode;

                _countryCode = GeoIP2.Country(IP);
                if (_countryCode == null)
                {
                    _countryCode = string.Empty;
                    return null;
                }

                return _countryCode;
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                    _countryCode = value;
            }
        }
        #endregion

        public AccsUser user { get; set; }

        public string user_uid { get; set; }

        public Dictionary<string, object> @params { get; set; }
    }
}
