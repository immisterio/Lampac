namespace Shared.Model.Online.VideoDB
{
    public class Folder
    {
        public string title { get; set; }

        public List<Folder> folder { get; set; }

        public string comment { get; set; }

        public string file { get; set; }
    }
}
