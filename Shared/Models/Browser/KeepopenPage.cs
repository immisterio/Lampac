using Microsoft.Playwright;
using System;

namespace Shared.Models.Browser
{
    public class KeepopenPage
    {
        public IPage page { get; set; }

        public bool busy { get; set; }

        public DateTime lockTo { get; set; }

        public DateTime lastActive { get; set; } = DateTime.Now;
    }
}
