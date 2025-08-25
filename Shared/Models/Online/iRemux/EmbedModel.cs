namespace Shared.Models.Online.iRemux
{
    public class EmbedModel
    {
        public bool IsEmpty { get; set; }

        public string content { get; set; }

        public List<Similar> similars { get; set; } = new List<Similar>(15);
    }
}
