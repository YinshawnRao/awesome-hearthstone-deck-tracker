namespace HearthstoneDeckTracker.Core.State;

/// <summary>
/// Cards still in a deck (friendly) or revealed from an opposing deck. Stub — no draw/play math yet.
/// </summary>
public sealed class DeckRemaining
{
    private readonly List<CardStack> _cards = [];

    public DeckRemaining(bool isFriendly)
    {
        IsFriendly = isFriendly;
    }

    public bool IsFriendly { get; }

    public IReadOnlyList<CardStack> Cards => _cards;

    public int TotalCards => _cards.Sum(c => c.Count);

    /// <summary>
    /// TODO: Seed from a parsed deckstring / in-game DECK zone FULL_ENTITY list.
    /// </summary>
    public void ReplaceAll(IEnumerable<CardStack> cards)
    {
        _cards.Clear();
        _cards.AddRange(cards);
    }

    /// <summary>
    /// TODO: Decrement after a friendly draw or known opponent play once the Power.log state machine exists.
    /// </summary>
    public void Decrement(string cardId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        var index = _cards.FindIndex(c => string.Equals(c.CardId, cardId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return;

        var current = _cards[index];
        if (current.Count <= 1)
            _cards.RemoveAt(index);
        else
            _cards[index] = current with { Count = current.Count - 1 };
    }
}
