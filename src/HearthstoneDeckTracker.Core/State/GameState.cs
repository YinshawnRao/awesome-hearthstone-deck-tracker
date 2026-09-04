namespace HearthstoneDeckTracker.Core.State;

/// <summary>
/// In-match snapshot the UI binds to. Driven by <c>PowerLogGameStateBuilder</c> from
/// CREATE_GAME / FULL_ENTITY / SHOW_ENTITY / TAG_CHANGE packets.
/// </summary>
public sealed class GameState
{
    private readonly List<TrackedCard> _friendlyHand = [];
    private readonly List<TrackedCard> _cardsPlayed = [];

    public bool IsInGame { get; set; }

    public int Turn { get; set; }

    /// <summary>CONTROLLER tag (1 or 2) of the local client, inferred from DECK reveals.</summary>
    public int? FriendlyController { get; set; }

    public int? OpponentController { get; set; }

    public DeckRemaining FriendlyDeck { get; } = new(isFriendly: true);

    /// <summary>Opponent cards the player has already seen (paper-and-pencil equivalent).</summary>
    public DeckRemaining OpponentSeen { get; } = new(isFriendly: false);

    public IReadOnlyList<TrackedCard> FriendlyHand => _friendlyHand;

    /// <summary>Friendly plays plus opponent cards revealed as they enter PLAY.</summary>
    public IReadOnlyList<TrackedCard> CardsPlayed => _cardsPlayed;

    public void ResetMatch()
    {
        IsInGame = false;
        Turn = 0;
        FriendlyController = null;
        OpponentController = null;
        FriendlyDeck.ReplaceAll([]);
        OpponentSeen.ReplaceAll([]);
        _friendlyHand.Clear();
        _cardsPlayed.Clear();
    }

    internal void AddToFriendlyHand(TrackedCard card)
    {
        if (_friendlyHand.Exists(c => c.EntityId == card.EntityId))
            return;
        _friendlyHand.Add(card);
    }

    internal void RemoveFromFriendlyHand(int entityId) =>
        _friendlyHand.RemoveAll(c => c.EntityId == entityId);

    internal void AddPlayed(TrackedCard card)
    {
        if (_cardsPlayed.Exists(c => c.EntityId == card.EntityId))
            return;
        _cardsPlayed.Add(card);
    }
}
