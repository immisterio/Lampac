namespace Shared.Models.Online.Kinobase
{
    public class EmbedModel
    {
        public bool IsEmpty { get; set; }
        public string errormsg { get; set; }


        public string content { get; set; }

        public Season[] serial { get; set; }

        public string quality { get; set; }
    }
}
