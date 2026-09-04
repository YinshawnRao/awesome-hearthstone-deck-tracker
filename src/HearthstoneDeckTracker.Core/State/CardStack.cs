namespace HearthstoneDeckTracker.Core.State;

/// <summary>
/// One card id and remaining count. Card names/dbfIds come from HearthstoneJSON / HearthDb later.
/// </summary>
public sealed record CardStack(string CardId, int Count, string? Name = null);
