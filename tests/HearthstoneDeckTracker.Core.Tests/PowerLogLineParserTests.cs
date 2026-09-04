using HearthstoneDeckTracker.Core.Logging;
using Xunit;

namespace HearthstoneDeckTracker.Core.Tests;

public class PowerLogLineParserTests
{
    [Theory]
    [InlineData(
        "D 12:00:00.0000000 GameState.DebugPrintPower() - TAG_CHANGE Entity=GameEntity tag=STEP value=MAIN_READY",
        PowerLogLineKind.TagChange)]
    [InlineData(
        "D 12:00:00.0000000 GameState.DebugPrintPower() - SHOW_ENTITY - Updating Entity=[entityName=UNKNOWN ENTITY id=12] CardID=EX1_169",
        PowerLogLineKind.ShowEntity)]
    [InlineData(
        "D 12:00:00.0000000 GameState.DebugPrintPower() - FULL_ENTITY - Updating [entityName=UNKNOWN ENTITY id=13 zone=DECK] CardID=",
        PowerLogLineKind.FullEntity)]
    [InlineData(
        "D 12:00:00.0000000 GameState.DebugPrintPower() - BLOCK_START BlockType=PLAY Entity=... EffectCardId=System.Collections.Generic.List",
        PowerLogLineKind.Other)]
    [InlineData("", PowerLogLineKind.Other)]
    public void Classify_KnownOpcodes(string line, PowerLogLineKind expected)
    {
        var parsed = PowerLogLineParser.Parse(line);
        Assert.Equal(expected, parsed.Kind);
        Assert.Equal(line, parsed.Raw);
    }
}
