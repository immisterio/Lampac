using System.Diagnostics;

namespace Shared.Models.DLNA
{
    public class CoverSettings
    {
        public bool enable { get; set; }

        public bool consoleLog { get; set; }

        public bool preview { get; set; }

        public int timeout { get; set; }

        public int skipModificationTime { get; set; }

        public string extension { get; set; }

        public string coverComand { get; set; }

        public string previewComand { get; set; }

        public ProcessPriorityClass? priorityClass { get; set; }
    }
}
