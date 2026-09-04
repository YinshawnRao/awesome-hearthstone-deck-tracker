namespace HearthstoneDeckTracker.Core.State;

/// <summary>
/// One card id and remaining count. zhCN names and art are resolved via <c>HearthstoneJsonCatalog</c>.
/// </summary>
public sealed record CardStack(string CardId, int Count, string? Name = null);
