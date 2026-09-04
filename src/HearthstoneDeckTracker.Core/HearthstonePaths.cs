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

    /// <summary>
    /// Placeholder or empty values are not a real filesystem path — the UI should
    /// show a status instead of starting a tail.
    /// </summary>
    public static bool IsUsablePowerLogPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        var trimmed = path.Trim().Trim('"');
        if (trimmed.Contains('<', StringComparison.Ordinal) || trimmed.Contains('>', StringComparison.Ordinal))
            return false;
        return trimmed.IndexOfAny(Path.GetInvalidPathChars()) < 0;
    }

    public static IEnumerable<string> CandidatePowerLogPaths()
    {
        foreach (var install in new[]
                 {
                     @"C:\Program Files (x86)\Hearthstone",
                     @"C:\Program Files\Hearthstone",
                 })
        {
            yield return CombinePowerLogPath(install);
        }
    }

    public static string? FindExistingPowerLog()
    {
        foreach (var candidate in CandidatePowerLogPaths())
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
