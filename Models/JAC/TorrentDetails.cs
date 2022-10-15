using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Lampac.Models.JAC
{
    public class TorrentDetails
    {
        public string trackerName { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string[] types { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string url { get; set; }


        public string title { get; set; }

        public int sid { get; set; }

        public int pir { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public double size { get; set; }

        public string sizeName { get; set; }

        public DateTime createTime { get; set; } = DateTime.Now;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string magnet { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string parselink { get; set; }



        [System.Text.Json.Serialization.JsonIgnore]
        public string name { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string originalname { get; set; }

        public int relased { get; set; }



        #region Быстрая сортировка
        [System.Text.Json.Serialization.JsonIgnore]
        public int quality { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string videotype { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public HashSet<string> voices { get; set; } = new HashSet<string>();

        [System.Text.Json.Serialization.JsonIgnore]
        public HashSet<int> seasons { get; set; } = new HashSet<int>();
        #endregion
    }
}
