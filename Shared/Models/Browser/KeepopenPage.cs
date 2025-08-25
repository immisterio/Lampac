using Microsoft.Playwright;

namespace Shared.Models.Browser
{
    public class KeepopenPage
    {
        #region Firefox
        public IPage page { get; set; }

        public bool busy { get; set; }

        public DateTime lockTo { get; set; }
        #endregion


        public IBrowserContext context { get; set; }

        public DateTime lastActive { get; set; } = DateTime.Now;

        public DateTime create { get; set; } = DateTime.Now;


        public string plugin { get; set; }

        public  (string ip, string username, string password) proxy { get; set; }
    }
}
