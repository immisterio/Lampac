using JacRed.Engine;
using System;

namespace JacRed.Models
{
    public class WriteTaskModel
    {
        public FileDB db { get; set; }

        public int openconnection { get; set; }

        public DateTime lastread { get; set; } = DateTime.UtcNow;
    }
}
