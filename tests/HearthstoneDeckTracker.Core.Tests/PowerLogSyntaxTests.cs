using HearthstoneDeckTracker.Core.Logging;

namespace HearthstoneDeckTracker.Core.Tests;

public class PowerLogSyntaxTests
{
    [Fact]
    public void TrySplit_ReadsMethodAndIndentedPayload()
    {
        const string line =
            "D 12:00:00.0000000 GameState.DebugPrintPower() -     tag=ZONE value=HAND";

        Assert.True(PowerLogSyntax.TrySplit(line, out var method, out var payload));
        Assert.Equal("GameState.DebugPrintPower", method);
        Assert.Equal("    tag=ZONE value=HAND", payload);
    }

    [Fact]
    public void TryParseShowEntity_NestedBrackets()
    {
        const string payload =
            "SHOW_ENTITY - Updating Entity=[entityName=UNKNOWN ENTITY [cardType=INVALID] id=12 zone=DECK zonePos=0 cardId= player=1] CardID=EX1_169";

        Assert.True(PowerLogSyntax.TryParseShowEntity(payload, out var entityId, out var cardId));
        Assert.Equal(12, entityId);
        Assert.Equal("EX1_169", cardId);
    }

    [Fact]
    public void TryParseTagChange_GameEntityAndNumericZones()
    {
        Assert.True(PowerLogSyntax.TryParseTagChange(
            "TAG_CHANGE Entity=GameEntity tag=STATE value=RUNNING",
            out var entityId,
            out var tag,
            out var value));
        Assert.Equal(1, entityId);
        Assert.Equal("STATE", tag);
        Assert.Equal("RUNNING", value);
        Assert.Equal("HAND", PowerLogSyntax.NormalizeZone("3"));
        Assert.Equal("DECK", PowerLogSyntax.NormalizeZone("DECK"));
    }
}
