namespace HearthstoneDeckTracker.Core;

/// <summary>
/// Well-known Hearthstone filesystem locations. Install-dir discovery is not implemented yet.
/// </summary>
public static class HearthstonePaths
{
    public const string PowerLogFileName = "Power.log";

    /// <summary>
    /// Default Windows path: <c>%LOCALAPPDATA%\Blizzard\Hearthstone</c>.
    /// On non-Windows this still uses <see cref="Environment.SpecialFolder.LocalApplicationData"/>.
    /// </summary>
    public static string DefaultLogConfigDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Blizzard",
            "Hearthstone");

    public static string DefaultLogConfigPath =>
        Path.Combine(DefaultLogConfigDirectory, "log.config");

    /// <summary>
    /// Placeholder for the live log. Real resolution (Battle.net install path / CN client) is out of scope.
    /// </summary>
    public static string PowerLogPathPlaceholder =>
        Path.Combine("<Hearthstone install>", "Logs", PowerLogFileName);

    public static string CombinePowerLogPath(string hearthstoneInstallDirectory) =>
        Path.Combine(hearthstoneInstallDirectory, "Logs", PowerLogFileName);
}
