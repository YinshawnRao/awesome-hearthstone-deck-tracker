using System.Globalization;
using System.Text.RegularExpressions;

namespace HearthstoneDeckTracker.Core.Logging;

/// <summary>
/// Small Power.log token helpers. Line shape follows the HearthSim game-state protocol
/// dump: <c>D HH:MM:SS.fffffff Source.Method() - PAYLOAD</c>.
/// </summary>
public static partial class PowerLogSyntax
{
    public const string GameStatePowerMethod = "GameState.DebugPrintPower";
    public const string GameStateGameMethod = "GameState.DebugPrintGame";

    [GeneratedRegex(@"\bid=(\d+)\b", RegexOptions.CultureInvariant)]
    private static partial Regex EntityIdInBracketsRegex();

    [GeneratedRegex(@"^Player EntityID=(\d+) PlayerID=(\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex PlayerHeaderRegex();

    [GeneratedRegex(@"^GameEntity EntityID=(\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex GameEntityHeaderRegex();

    [GeneratedRegex(@"^PlayerID=(\d+),\s*PlayerName=(.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex DebugPrintGamePlayerRegex();

    /// <summary>
    /// Splits a raw log line into <c>Source.Method</c> and the payload after <c>() - </c>.
    /// Lines that are not Power.log formatted return the whole trimmed line as payload.
    /// </summary>
    public static bool TrySplit(string? line, out string? sourceMethod, out string payload)
    {
        sourceMethod = null;
        payload = string.Empty;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var raw = line.TrimEnd();
        var dash = raw.IndexOf("() - ", StringComparison.Ordinal);
        if (dash < 0)
        {
            payload = raw.Trim();
            return false;
        }

        payload = raw[(dash + 5)..];
        var methodStart = raw.LastIndexOf(' ', dash);
        if (methodStart >= 0 && methodStart < dash)
            sourceMethod = raw[(methodStart + 1)..dash];
        else
            sourceMethod = raw[..dash];

        return true;
    }

    public static bool IsGameStatePower(string? sourceMethod) =>
        string.Equals(sourceMethod, GameStatePowerMethod, StringComparison.Ordinal);

    public static bool IsGameStateGame(string? sourceMethod) =>
        string.Equals(sourceMethod, GameStateGameMethod, StringComparison.Ordinal);

    public static bool TryReadAssignedValue(string text, string key, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(key))
            return false;

        var token = key + "=";
        var index = 0;
        while (index < text.Length)
        {
            var found = text.IndexOf(token, index, StringComparison.Ordinal);
            if (found < 0)
                return false;

            var before = found == 0 ? ' ' : text[found - 1];
            if (char.IsLetterOrDigit(before) || before == '_')
            {
                index = found + 1;
                continue;
            }

            var start = found + token.Length;
            var end = start;
            while (end < text.Length && !char.IsWhiteSpace(text[end]))
                end++;

            value = text[start..end];
            return true;
        }

        return false;
    }

    public static bool TryParseEntityId(string? entity, out int entityId)
    {
        entityId = 0;
        if (string.IsNullOrWhiteSpace(entity))
            return false;

        var text = entity.Trim();
        if (text.Equals("GameEntity", StringComparison.OrdinalIgnoreCase))
        {
            entityId = 1;
            return true;
        }

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out entityId))
            return entityId > 0;

        var match = EntityIdInBracketsRegex().Match(text);
        if (!match.Success)
            return false;

        entityId = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        return entityId > 0;
    }

    public static bool TryParseCreateEntity(string payload, out int entityId, out string? cardId)
    {
        entityId = 0;
        cardId = null;
        if (!payload.Contains("FULL_ENTITY", StringComparison.Ordinal))
            return false;

        if (TryReadAssignedValue(payload, "ID", out var idText)
            && int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out entityId)
            && entityId > 0)
        {
            cardId = ReadOptionalCardId(payload);
            return true;
        }

        var entitySpan = ExtractEntityToken(payload, "Updating ");
        if (entitySpan is not null && TryParseEntityId(entitySpan, out entityId))
        {
            cardId = ReadOptionalCardId(payload);
            return true;
        }

        return false;
    }

    public static bool TryParseShowEntity(string payload, out int entityId, out string? cardId)
    {
        entityId = 0;
        cardId = null;
        var entitySpan = ExtractEntityToken(payload, "Entity=");
        if (entitySpan is null || !TryParseEntityId(entitySpan, out entityId))
            return false;

        cardId = ReadOptionalCardId(payload);
        return true;
    }

    public static bool TryParseHideEntity(string payload, out int entityId, out string tag, out string value)
    {
        entityId = 0;
        tag = string.Empty;
        value = string.Empty;
        var entitySpan = ExtractEntityToken(payload, "Entity=");
        if (entitySpan is null || !TryParseEntityId(entitySpan, out entityId))
            return false;

        TryReadAssignedValue(payload, "tag", out tag);
        TryReadAssignedValue(payload, "value", out value);
        value = StripDefChange(value);
        return tag.Length > 0;
    }

    public static bool TryParseTagChange(string payload, out int entityId, out string tag, out string value)
    {
        entityId = 0;
        tag = string.Empty;
        value = string.Empty;

        const string prefix = "TAG_CHANGE Entity=";
        var start = payload.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0)
            return false;

        start += prefix.Length;
        var tagAt = payload.IndexOf(" tag=", start, StringComparison.Ordinal);
        if (tagAt < 0)
            return false;

        var entity = payload[start..tagAt];
        if (!TryParseEntityId(entity, out entityId))
            return false;

        if (!TryReadAssignedValue(payload[tagAt..], "tag", out tag))
            return false;

        TryReadAssignedValue(payload[tagAt..], "value", out value);
        value = StripDefChange(value);
        return tag.Length > 0;
    }

    public static bool TryParseTagValue(string payload, out string tag, out string value)
    {
        tag = string.Empty;
        value = string.Empty;
        var text = payload.Trim();
        if (!text.StartsWith("tag=", StringComparison.Ordinal))
            return false;

        if (!TryReadAssignedValue(text, "tag", out tag))
            return false;
        TryReadAssignedValue(text, "value", out value);
        value = StripDefChange(value);
        return tag.Length > 0;
    }

    public static bool TryParseGameEntityHeader(string payload, out int entityId)
    {
        entityId = 0;
        var match = GameEntityHeaderRegex().Match(payload.Trim());
        if (!match.Success)
            return false;
        entityId = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        return entityId > 0;
    }

    public static bool TryParsePlayerHeader(string payload, out int entityId, out int playerId)
    {
        entityId = 0;
        playerId = 0;
        var match = PlayerHeaderRegex().Match(payload.Trim());
        if (!match.Success)
            return false;
        entityId = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        playerId = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        return entityId > 0 && playerId > 0;
    }

    public static bool TryParseDebugPrintGamePlayer(string payload, out int playerId, out string name)
    {
        playerId = 0;
        name = string.Empty;
        var match = DebugPrintGamePlayerRegex().Match(payload.Trim());
        if (!match.Success)
            return false;
        playerId = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        name = match.Groups[2].Value.Trim();
        return playerId > 0;
    }

    public static string? ReadOptionalCardId(string text)
    {
        if (!TryReadAssignedValue(text, "CardID", out var cardId) &&
            !TryReadAssignedValue(text, "cardId", out cardId))
            return null;

        return string.IsNullOrWhiteSpace(cardId) ? null : cardId;
    }

    public static string NormalizeZone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "INVALID";

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            return numeric switch
            {
                1 => "PLAY",
                2 => "DECK",
                3 => "HAND",
                4 => "GRAVEYARD",
                5 => "REMOVEDFROMGAME",
                6 => "SETASIDE",
                7 => "SECRET",
                _ => "INVALID",
            };
        }

        return value.Trim().ToUpperInvariant();
    }

    public static string NormalizeCardType(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    public static bool TryParseInt(string? value, out int number) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);

    private static string StripDefChange(string value)
    {
        const string suffix = "DEF CHANGE";
        if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return value[..^suffix.Length].Trim();
        return value;
    }

    /// <summary>
    /// Reads the entity token after <paramref name="marker"/>, including bracket forms
    /// that nest <c>[cardType=INVALID]</c>.
    /// </summary>
    private static string? ExtractEntityToken(string payload, string marker)
    {
        var start = payload.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            return null;

        start += marker.Length;
        if (start >= payload.Length)
            return null;

        if (payload[start] != '[')
        {
            var end = start;
            while (end < payload.Length && !char.IsWhiteSpace(payload[end]))
                end++;
            return payload[start..end];
        }

        var depth = 0;
        for (var i = start; i < payload.Length; i++)
        {
            if (payload[i] == '[')
                depth++;
            else if (payload[i] == ']')
            {
                depth--;
                if (depth == 0)
                    return payload[start..(i + 1)];
            }
        }

        return payload[start..];
    }
}
