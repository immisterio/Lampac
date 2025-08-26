namespace SISI
{
    public class ModInit
    {
        public static void loaded()
        {
            Directory.CreateDirectory("wwwroot/bookmarks/img");
            Directory.CreateDirectory("wwwroot/bookmarks/preview");
        }
    }
}
