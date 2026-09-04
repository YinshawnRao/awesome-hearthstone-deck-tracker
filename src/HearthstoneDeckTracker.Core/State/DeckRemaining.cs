namespace HearthstoneDeckTracker.Core.State;

/// <summary>
/// Cards still in a deck (friendly) or revealed from an opposing deck.
/// Unknown copies (FULL_ENTITY with empty CardID) live in <see cref="UnknownCount"/>.
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

    public int UnknownCount { get; private set; }

    public int TotalCards => _cards.Sum(c => c.Count) + UnknownCount;

    public void ReplaceAll(IEnumerable<CardStack> cards)
    {
        _cards.Clear();
        UnknownCount = 0;
        _cards.AddRange(cards);
    }

    public void Add(string cardId, int count = 1, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        if (count <= 0)
            return;

        var index = IndexOf(cardId);
        if (index < 0)
            _cards.Add(new CardStack(cardId, count, name));
        else
            _cards[index] = _cards[index] with { Count = _cards[index].Count + count };
    }

    public void AddUnknown(int count = 1)
    {
        if (count > 0)
            UnknownCount += count;
    }

    /// <summary>Turn one unknown copy into a known card id (SHOW_ENTITY while still in DECK).</summary>
    public void PromoteUnknown(string cardId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        if (UnknownCount > 0)
            UnknownCount--;
        Add(cardId);
    }

    public void Decrement(string cardId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        RemoveOne(cardId);
    }

    /// <summary>Remove one copy by id when known, otherwise drop an unknown copy.</summary>
    public void RemoveOne(string? cardId)
    {
        if (!string.IsNullOrWhiteSpace(cardId))
        {
            var index = IndexOf(cardId);
            if (index >= 0)
            {
                var current = _cards[index];
                if (current.Count <= 1)
                    _cards.RemoveAt(index);
                else
                    _cards[index] = current with { Count = current.Count - 1 };
                return;
            }
        }

        if (UnknownCount > 0)
            UnknownCount--;
    }

    private int IndexOf(string cardId) =>
        _cards.FindIndex(c => string.Equals(c.CardId, cardId, StringComparison.OrdinalIgnoreCase));
}
