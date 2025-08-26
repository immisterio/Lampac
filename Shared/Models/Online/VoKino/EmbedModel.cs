namespace Shared.Models.Online.VoKino
{
    public class EmbedModel
    {
        public bool IsEmpty { get; set; }

        public Сhannel[] menu { get; set; }

        public Сhannel[] channels { get; set; }

        public List<Similar> similars { get; set; }
    }
}
