namespace Shared.Models.SISI.Base
{
    public class ModelItem
    {
        public ModelItem() { }

        public ModelItem(string name, string uri)
        {
            this.uri = uri;
            this.name = name;
        }

        public string uri { get; set; }

        public string name { get; set; }
    }
}
