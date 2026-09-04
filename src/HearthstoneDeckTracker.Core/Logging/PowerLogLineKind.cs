namespace HearthstoneDeckTracker.Core.Logging;

/// <summary>
/// Coarse Power.log opcode classification. Not a full packet / entity state machine.
/// </summary>
public enum PowerLogLineKind
{
    Other = 0,
    TagChange,
    ShowEntity,
    FullEntity,
}
