namespace HearthstoneDeckTracker.Core.Cards;

/// <summary>
/// User-local disk cache for the zhCN card catalog and rendered card art.
/// Default root: <c>%LOCALAPPDATA%\AwesomeHearthstoneDeckTracker\cache\</c>.
/// </summary>
public static class AppCachePaths
{
    public const string AppFolderName = "AwesomeHearthstoneDeckTracker";

    public static string CacheRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolderName,
            "cache");

    public static string CatalogJson => Path.Combine(CacheRoot, "cards.zhCN.json");

    public static string ArtDirectory => Path.Combine(CacheRoot, "art", "zhCN", "256x");

    public static void EnsureDirectory(string path) => Directory.CreateDirectory(path);
}
