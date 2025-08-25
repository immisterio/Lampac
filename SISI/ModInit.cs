using Newtonsoft.Json;
using Shared.Models.SISI;

namespace SISI
{
    public class ModInit
    {
        public static void loaded()
        {
            Directory.CreateDirectory("wwwroot/bookmarks/img");
            Directory.CreateDirectory("wwwroot/bookmarks/preview");

            try
            {
                // migrate old bookmarks
                foreach (string folder in Directory.GetDirectories(Path.Combine("cache", "bookmarks", "sisi")))
                {
                    string folderName = Path.GetFileName(folder);
                    foreach (string file in Directory.GetFiles(folder))
                    {
                        try
                        {
                            string md5user = folderName + Path.GetFileName(file);
                            var bookmarks = JsonConvert.DeserializeObject<List<PlaylistItem>>(File.ReadAllText(file));

                            if (bookmarks.Count > 0)
                            {
                                CollectionDb.sisi_users.Insert(new User
                                {
                                    Id = md5user,
                                    Bookmarks = bookmarks
                                });
                            }
                        }
                        catch { }
                    }

                    try
                    {
                        Directory.Delete(folder, true);
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
