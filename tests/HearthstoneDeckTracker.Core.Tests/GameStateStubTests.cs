using HearthstoneDeckTracker.Core.State;
using Xunit;

namespace HearthstoneDeckTracker.Core.Tests;

public class GameStateStubTests
{
    [Fact]
    public void Decrement_RemovesLastCopy()
    {
        var state = new GameState();
        state.FriendlyDeck.ReplaceAll([new CardStack("EX1_169", 2, "Innervate")]);
        state.FriendlyDeck.Decrement("EX1_169");

        Assert.Equal(1, state.FriendlyDeck.TotalCards);
        state.FriendlyDeck.Decrement("EX1_169");
        Assert.Equal(0, state.FriendlyDeck.TotalCards);

        state.ResetMatch();
        Assert.False(state.IsInGame);
        Assert.Empty(state.FriendlyDeck.Cards);
    }
}
