namespace HearthstoneDeckTracker.Core.Logging;

/// <summary>
/// Coarse Power.log opcode classification. Packet bodies (indented <c>tag=</c> lines)
/// are applied by <see cref="PowerLogGameStateBuilder"/>.
/// </summary>
public enum PowerLogLineKind
{
    Other = 0,
    TagChange,
    ShowEntity,
    FullEntity,
    CreateGame,
    HideEntity,
    TagValue,
    GameEntity,
    PlayerEntity,
}
