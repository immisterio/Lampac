using Shared.Model.SISI;

namespace Lampac.Models.AppConf
{
    public class SisiConf
    {
        public bool xdb { get; set; }

        public bool rsize { get; set; }

        public string? rsize_host { get; set; }

        public string[]? rsize_disable { get; set; }

        public int heightPicture { get; set; }

        public int widthPicture { get; set; }


        public string? component { get; set; }

        public string? iconame { get; set; }


        public BookmarksConf bookmarks { get; set; } = new BookmarksConf();
}
}
