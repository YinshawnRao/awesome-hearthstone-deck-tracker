namespace HearthstoneDeckTracker.Core.Logging;

/// <summary>
/// Classifies a single Power.log line by opcode. Does not track entities, zones, or games.
/// </summary>
public static class PowerLogLineParser
{
    public static ClassifiedPowerLogLine Parse(string? line)
    {
        line ??= string.Empty;
        return new ClassifiedPowerLogLine(Classify(line), line);
    }

    public static PowerLogLineKind Classify(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return PowerLogLineKind.Other;

        // Typical shape: "D HH:MM:SS.fffffff GameState.DebugPrintPower() - TAG_CHANGE ..."
        var payload = ExtractPayload(line);

        // TODO: Replace substring checks with a tokenizer (CREATE_GAME, BLOCK_*, META_DATA, ...).
        if (ContainsOpcode(payload, "TAG_CHANGE"))
            return PowerLogLineKind.TagChange;
        if (ContainsOpcode(payload, "SHOW_ENTITY"))
            return PowerLogLineKind.ShowEntity;
        if (ContainsOpcode(payload, "FULL_ENTITY"))
            return PowerLogLineKind.FullEntity;

        return PowerLogLineKind.Other;
    }

    private static string ExtractPayload(string line)
    {
        var separator = line.IndexOf(" - ", StringComparison.Ordinal);
        return separator >= 0 ? line[(separator + 3)..] : line;
    }

    private static bool ContainsOpcode(string payload, string opcode) =>
        payload.StartsWith(opcode, StringComparison.Ordinal)
        || payload.Contains(opcode + ' ', StringComparison.Ordinal)
        || payload.Contains(opcode + '-', StringComparison.Ordinal);
}
