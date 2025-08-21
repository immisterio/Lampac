namespace Shared.Model.SISI
{
    public struct ModelItem
    {
        public ModelItem(string name, string uri)
        {
            this.uri = uri;
            this.name = name;
        }

        public string uri { get; set; }

        public string name { get; set; }
    }
}
