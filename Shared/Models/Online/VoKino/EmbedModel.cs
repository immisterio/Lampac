namespace Shared.Model.Online.VoKino
{
    public class EmbedModel
    {
        public bool IsEmpty { get; set; }

        public List<Сhannel>? menu { get; set; }

        public List<Сhannel>? channels { get; set; }

        public List<Similar>? similars { get; set; }
    }
}
