namespace HearthstoneDeckTracker.Core.Logging;

/// <summary>
/// Classifies a single Power.log line by opcode. Does not track entities, zones, or games.
/// </summary>
public static class PowerLogLineParser
{
    public static ClassifiedPowerLogLine Parse(string? line)
    {
        line ??= string.Empty;
        PowerLogSyntax.TrySplit(line, out var sourceMethod, out var payload);
        return new ClassifiedPowerLogLine(ClassifyPayload(payload), line, payload, sourceMethod);
    }

    public static PowerLogLineKind Classify(string? line) => Parse(line).Kind;

    public static PowerLogLineKind ClassifyPayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return PowerLogLineKind.Other;

        var text = payload.Trim();

        if (text.Equals("CREATE_GAME", StringComparison.Ordinal))
            return PowerLogLineKind.CreateGame;
        if (StartsWithOpcode(text, "TAG_CHANGE"))
            return PowerLogLineKind.TagChange;
        if (StartsWithOpcode(text, "SHOW_ENTITY"))
            return PowerLogLineKind.ShowEntity;
        if (StartsWithOpcode(text, "FULL_ENTITY"))
            return PowerLogLineKind.FullEntity;
        if (StartsWithOpcode(text, "HIDE_ENTITY"))
            return PowerLogLineKind.HideEntity;
        if (text.StartsWith("GameEntity ", StringComparison.Ordinal))
            return PowerLogLineKind.GameEntity;
        if (text.StartsWith("Player EntityID=", StringComparison.Ordinal))
            return PowerLogLineKind.PlayerEntity;
        if (text.StartsWith("tag=", StringComparison.Ordinal))
            return PowerLogLineKind.TagValue;

        return PowerLogLineKind.Other;
    }

    private static bool StartsWithOpcode(string payload, string opcode) =>
        payload.StartsWith(opcode, StringComparison.Ordinal)
        && (payload.Length == opcode.Length
            || payload[opcode.Length] is ' ' or '-');
}
