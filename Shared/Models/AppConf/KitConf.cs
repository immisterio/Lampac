﻿using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Shared.Models.AppConf
{
    public class KitConf
    {
        public bool enable { get; set; }

        public string path { get; set; }

        public bool IsAllUsersPath { get; set; }

        public int cacheToSeconds { get; set; }

        public bool rhub_fallback { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public Dictionary<string, JObject> allUsers { get; set; }
    }
}
