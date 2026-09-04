namespace HearthstoneDeckTracker.Core.Cards;

/// <summary>
/// One HearthstoneJSON card, indexed by the Power.log <c>CardID</c> string (e.g. <c>CS2_029</c>).
/// </summary>
public sealed record CardInfo(
    string CardId,
    string? Name,
    int? Cost,
    string? Rarity,
    string? CardClass,
    string? Type,
    string ArtUrl);
