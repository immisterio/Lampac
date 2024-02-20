namespace Shared.Model.Online
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
    }
}
