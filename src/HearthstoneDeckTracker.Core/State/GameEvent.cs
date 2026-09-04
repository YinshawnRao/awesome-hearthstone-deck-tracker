namespace HearthstoneDeckTracker.Core.State;

public enum GameEventKind
{
    GameCreated = 0,
    Draw,
    Play,
    Reveal,
    ZoneChange,
    GameEnded,
}

/// <summary>Paper-and-pencil event derived from a Power.log zone / reveal packet.</summary>
public sealed record GameEvent(
    GameEventKind Kind,
    int EntityId,
    string? CardId,
    int Controller,
    string? FromZone,
    string? ToZone,
    string Summary);
