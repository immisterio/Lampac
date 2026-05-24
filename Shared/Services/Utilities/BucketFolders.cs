namespace Shared.Services.Utilities;

public static class BucketFolders
{
    private const string alpha = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    private static readonly IReadOnlyDictionary<char, string> Names = alpha
        .Select((c, i) => new { Char = c, FolderName = i.ToString("D2") })
        .ToDictionary(x => x.Char, x => x.FolderName);

    public static void Create(string root)
    {
        Directory.CreateDirectory(root);

        foreach (string folderName in Names.Values)
            Directory.CreateDirectory(Path.Combine(root, folderName));
    }

    public static string Name(char c)
    {
        if (!Names.TryGetValue(c, out string folderName))
            return "00";

        return folderName;
    }
}
