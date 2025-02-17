﻿namespace Shared.Model.Online
{
    public class HeadersModel
    {
        public HeadersModel(string name, string val)
        {
            this.name = name;
            this.val = val;
        }

        public string name { get; set; }

        public string val { get; set; }


        #region Init
        public static List<HeadersModel> Init(string name, string val)
        {
            return new List<HeadersModel>() { new HeadersModel(name, val)};
        }

        public static List<HeadersModel> Init(List<HeadersModel>? headers)
        {
            return headers ?? new List<HeadersModel>();
        }

        public static List<HeadersModel> Init(params (string name, string val)[] headers)
        {
            var h = new List<HeadersModel>(headers.Count());

            foreach (var i in headers)
                h.Add(new HeadersModel(i.name, i.val));

            return h;
        }

        public static List<HeadersModel> Init(Dictionary<string, string> headers)
        {
            if (headers == null || headers.Count == 0)
                return new List<HeadersModel>();

            var h = new List<HeadersModel>(headers.Count);

            foreach (var i in headers)
                h.Add(new HeadersModel(i.Key, i.Value));

            return h;
        }
        #endregion

        #region Join
        public static List<HeadersModel> Join(List<HeadersModel>? h1, List<HeadersModel>? h2)
        {
            if (h1 == null)
                return h2 ?? new List<HeadersModel>();

            if (h2 == null)
                return h1 ?? new List<HeadersModel>();

            var result = new List<HeadersModel>(h1);
            result.AddRange(h2);

            return result;
        }
        #endregion
    }
}
