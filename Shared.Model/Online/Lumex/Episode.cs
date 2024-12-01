namespace Shared.Model.Online.Lumex
{
    public class Episode
    {
        public int episode_id { get; set; }

        public string name { get; set; }

        public string poster { get; set; }

        public List<Medium> media { get; set; }
    }
}
