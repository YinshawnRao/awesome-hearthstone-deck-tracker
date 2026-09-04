using HearthstoneDeckTracker.Core.State;

namespace HearthstoneDeckTracker.Core.Cards;

/// <summary>UI-agnostic row for deck / hand / played / opponent-seen lists.</summary>
public sealed record CardDisplayRow(
    string CardId,
    string DisplayName,
    int? Cost,
    int Count,
    string? ArtPath,
    string Tooltip)
{
    public string CostText => Cost is int cost ? cost.ToString() : string.Empty;

    public string CountText => Count > 1 ? $"×{Count}" : string.Empty;

    public string PrimaryText =>
        Cost is int cost
            ? $"{cost}  {DisplayName}"
            : DisplayName;
}

/// <summary>Resolves Power.log CardIDs to zhCN names, cost, and locally cached art paths.</summary>
public sealed class CardDisplayFormatter
{
    private readonly HearthstoneJsonCatalog _catalog;
    private readonly CardArtCache? _artCache;

    public CardDisplayFormatter(HearthstoneJsonCatalog catalog, CardArtCache? artCache = null)
    {
        _catalog = catalog;
        _artCache = artCache;
    }

    public CardDisplayRow Format(string? cardId, int count = 1)
    {
        if (string.IsNullOrWhiteSpace(cardId))
        {
            return new CardDisplayRow(
                CardId: string.Empty,
                DisplayName: "未知",
                Cost: null,
                Count: count,
                ArtPath: null,
                Tooltip: "未知卡牌（Power.log 未给出 CardID）");
        }

        var info = _catalog.Get(cardId);
        var name = string.IsNullOrWhiteSpace(info?.Name) ? cardId : info!.Name!;
        var artPath = _artCache?.TryGetCachedPath(cardId);
        var tooltip = info is null
            ? cardId
            : $"{cardId} · {info.Type} · {info.CardClass} · {info.Rarity}";

        return new CardDisplayRow(cardId, name, info?.Cost, count, artPath, tooltip);
    }

    public CardDisplayRow Format(CardStack stack) => Format(stack.CardId, stack.Count);

    public CardDisplayRow Format(TrackedCard card) => Format(card.CardId);

    public IReadOnlyList<CardDisplayRow> FormatDeckRemaining(GameState state)
    {
        var rows = state.FriendlyDeck.Cards.Select(Format).ToList();
        if (state.FriendlyDeck.UnknownCount > 0)
            rows.Add(Format(cardId: null, count: state.FriendlyDeck.UnknownCount));
        return SortByCost(rows);
    }

    public IReadOnlyList<CardDisplayRow> FormatHand(GameState state) =>
        state.FriendlyHand.Select(Format).ToList();

    public IReadOnlyList<CardDisplayRow> FormatPlayed(GameState state) =>
        state.CardsPlayed.Select(Format).ToList();

    public IReadOnlyList<CardDisplayRow> FormatOpponentSeen(GameState state) =>
        SortByCost(state.OpponentSeen.Cards.Select(Format).ToList());

    public string FormatEventSummary(GameEvent ev)
    {
        if (string.IsNullOrWhiteSpace(ev.CardId))
            return ev.Summary;

        var name = _catalog.DisplayName(ev.CardId);
        if (string.Equals(name, ev.CardId, StringComparison.Ordinal))
            return ev.Summary;

        return ev.Summary.Replace(ev.CardId, name, StringComparison.Ordinal);
    }

    public IEnumerable<string> VisibleCardIds(GameState state)
    {
        foreach (var stack in state.FriendlyDeck.Cards)
            yield return stack.CardId;
        foreach (var card in state.FriendlyHand)
        {
            if (!string.IsNullOrWhiteSpace(card.CardId))
                yield return card.CardId;
        }

        foreach (var card in state.CardsPlayed)
        {
            if (!string.IsNullOrWhiteSpace(card.CardId))
                yield return card.CardId;
        }

        foreach (var stack in state.OpponentSeen.Cards)
            yield return stack.CardId;
    }

    private static List<CardDisplayRow> SortByCost(List<CardDisplayRow> rows) =>
        rows
            .OrderBy(r => r.Cost ?? 99)
            .ThenBy(r => r.DisplayName, StringComparer.Ordinal)
            .ToList();
}
