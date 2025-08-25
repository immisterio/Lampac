namespace JacRed.Models
{
    public class WriteTaskModel
    {
        public FileDB db { get; set; }

        public int openconnection { get; set; }

        public int countread { get; set; }

        public DateTime lastread { get; set; }

        public DateTime create { get; set; } = DateTime.Now;
    }
}
