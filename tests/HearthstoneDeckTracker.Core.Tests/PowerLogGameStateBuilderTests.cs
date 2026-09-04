using HearthstoneDeckTracker.Core.Logging;
using HearthstoneDeckTracker.Core.State;

namespace HearthstoneDeckTracker.Core.Tests;

public class PowerLogGameStateBuilderTests
{
    [Fact]
    public void MinimalFixture_UpdatesDrawPlayAndDeckRemaining()
    {
        var builder = LoadFixture("minimal_draw_play.log");
        var state = builder.State;

        Assert.True(state.IsInGame);
        Assert.Equal(1, state.Turn);
        Assert.Equal(1, state.FriendlyController);
        Assert.Equal(2, state.OpponentController);

        Assert.Equal(1, state.FriendlyDeck.TotalCards);
        Assert.Equal(1, state.FriendlyDeck.UnknownCount);
        Assert.Empty(state.FriendlyDeck.Cards);

        Assert.Empty(state.FriendlyHand);
        Assert.Equal(3, state.CardsPlayed.Count);
        Assert.Contains(state.CardsPlayed, c => c.CardId == "EX1_169" && c.Controller == 1);
        Assert.Contains(state.CardsPlayed, c => c.CardId == "CS2_172" && c.Controller == 1);
        Assert.Contains(state.CardsPlayed, c => c.CardId == "CS2_182" && c.Controller == 2);

        Assert.Equal(1, state.OpponentSeen.TotalCards);
        Assert.Contains(state.OpponentSeen.Cards, c => c.CardId == "CS2_182" && c.Count == 1);

        Assert.Contains(builder.Events, e => e.Kind == GameEventKind.Draw && e.CardId == "EX1_169" && e.Controller == 1);
        Assert.Contains(builder.Events, e => e.Kind == GameEventKind.Draw && e.CardId == "CS2_172" && e.Controller == 1);
        Assert.Contains(builder.Events, e => e.Kind == GameEventKind.Play && e.CardId == "EX1_169");
        Assert.Contains(builder.Events, e => e.Kind == GameEventKind.Play && e.CardId == "CS2_182" && e.Controller == 2);
        Assert.Contains(builder.Events, e => e.Kind == GameEventKind.Reveal && e.CardId == "CS2_182");

        // PowerTaskList duplicate of the Innervate play must not double-count.
        Assert.Equal(1, builder.Events.Count(e => e.Kind == GameEventKind.Play && e.CardId == "EX1_169"));
        Assert.Equal("SpikePlayer", builder.PlayerNames[1]);
    }

    [Fact]
    public void OpponentRevealFixture_RecordsSeenCardWithoutFriendlyHand()
    {
        var builder = LoadFixture("opponent_reveal.log");
        var state = builder.State;

        Assert.Equal(1, state.FriendlyController);
        Assert.Empty(state.FriendlyDeck.Cards);
        Assert.Equal(0, state.FriendlyDeck.TotalCards);
        Assert.Single(state.FriendlyHand);
        Assert.Equal("CS2_029", state.FriendlyHand[0].CardId);

        Assert.Contains(state.OpponentSeen.Cards, c => c.CardId == "EX1_562");
        Assert.Contains(state.CardsPlayed, c => c.CardId == "EX1_562" && c.Controller == 2);
        Assert.DoesNotContain(state.FriendlyHand, c => c.CardId == "EX1_562");
        Assert.Contains(builder.Events, e => e.Kind == GameEventKind.Draw && e.CardId == "CS2_029");
        Assert.Contains(builder.Events, e => e.Kind == GameEventKind.Play && e.CardId == "EX1_562");
    }

    [Fact]
    public void Ingest_IgnoresNullGarbageAndPowerTaskList()
    {
        var builder = new PowerLogGameStateBuilder();
        builder.Ingest((string?)null);
        builder.Ingest("");
        builder.Ingest("not a power log line");
        builder.Ingest("D 12:00:00.0000000 PowerTaskList.DebugPrintPower() - CREATE_GAME");
        builder.Ingest("D 12:00:00.0000000 PowerTaskList.DebugPrintPower() - TAG_CHANGE Entity=GameEntity tag=STATE value=RUNNING");

        Assert.False(builder.State.IsInGame);
        Assert.Empty(builder.Events);
    }

    [Fact]
    public void Ingest_ClassifiedLine_CreateGameResetsPriorState()
    {
        var builder = new PowerLogGameStateBuilder();
        builder.State.FriendlyDeck.Add("EX1_169", 2);
        builder.Ingest(PowerLogLineParser.Parse("D 12:00:00.0000000 GameState.DebugPrintPower() - CREATE_GAME"));
        builder.Ingest("D 12:00:00.0000001 GameState.DebugPrintPower() - TAG_CHANGE Entity=GameEntity tag=STATE value=RUNNING");

        Assert.True(builder.State.IsInGame);
        Assert.Equal(0, builder.State.FriendlyDeck.TotalCards);
        Assert.Contains(builder.Events, e => e.Kind == GameEventKind.GameCreated);
    }

    private static PowerLogGameStateBuilder LoadFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        Assert.True(File.Exists(path), $"Missing fixture {path}");
        var builder = new PowerLogGameStateBuilder();
        builder.IngestAll(File.ReadLines(path));
        return builder;
    }
}
