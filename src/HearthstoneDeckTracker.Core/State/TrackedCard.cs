namespace HearthstoneDeckTracker.Core.State;

/// <summary>A single known entity the tracker is showing (hand, play, or seen).</summary>
public sealed record TrackedCard(int EntityId, string? CardId, int Controller);
