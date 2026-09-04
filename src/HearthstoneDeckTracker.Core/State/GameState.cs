namespace HearthstoneDeckTracker.Core.State;

/// <summary>
/// In-match snapshot the UI will bind to. Stub only — do not treat this as a working tracker.
/// </summary>
public sealed class GameState
{
    public bool IsInGame { get; set; }

    public int Turn { get; set; }

    public DeckRemaining FriendlyDeck { get; } = new(isFriendly: true);

    /// <summary>Opponent cards the player has already seen (paper-and-pencil equivalent).</summary>
    public DeckRemaining OpponentSeen { get; } = new(isFriendly: false);

    /// <summary>
    /// TODO: Drive from CREATE_GAME / TAG_CHANGE STEP / zone moves in Power.log. No state machine yet.
    /// </summary>
    public void ResetMatch()
    {
        IsInGame = false;
        Turn = 0;
        FriendlyDeck.ReplaceAll([]);
        OpponentSeen.ReplaceAll([]);
    }
}
