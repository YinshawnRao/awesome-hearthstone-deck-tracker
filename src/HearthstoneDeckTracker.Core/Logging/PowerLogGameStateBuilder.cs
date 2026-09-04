using HearthstoneDeckTracker.Core.State;

namespace HearthstoneDeckTracker.Core.Logging;

/// <summary>
/// Incremental Power.log consumer. Applies CREATE_GAME / FULL_ENTITY / SHOW_ENTITY /
/// TAG_CHANGE (and their indented <c>tag=</c> bodies) onto a <see cref="GameState"/>.
/// </summary>
/// <remarks>
/// Spike limits: only <c>GameState.DebugPrintPower</c> / <c>DebugPrintGame</c> (PowerTaskList
/// is ignored so the same packets are not applied twice). Tracks DECK↔HAND↔PLAY↔GRAVEYARD
/// for paper-visible draws, plays, and opponent reveals. No secrets helper.
/// Card names/art are resolved by <c>HearthstoneJsonCatalog</c> in the UI, not here.
/// Protocol concepts: https://hearthsim.info/docs/gamestate-protocol/
/// </remarks>
public sealed class PowerLogGameStateBuilder
{
    public const int DefaultEventCapacity = 80;

    private readonly Dictionary<int, EntityRecord> _entities = [];
    private readonly List<GameEvent> _events = [];
    private readonly int _eventCapacity;
    private EntityRecord? _pending;

    public PowerLogGameStateBuilder(GameState? state = null, int eventCapacity = DefaultEventCapacity)
    {
        State = state ?? new GameState();
        _eventCapacity = eventCapacity > 0 ? eventCapacity : DefaultEventCapacity;
    }

    public GameState State { get; }

    public IReadOnlyList<GameEvent> Events => _events;

    private readonly Dictionary<int, string?> _playerNames = [];

    public IReadOnlyDictionary<int, string?> PlayerNames => _playerNames;

    public void IngestAll(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        foreach (var line in lines)
            Ingest(line);
    }

    public void Ingest(ClassifiedPowerLogLine line) => Apply(line);

    public void Ingest(string? rawLine) => Apply(PowerLogLineParser.Parse(rawLine));

    private void Apply(ClassifiedPowerLogLine line)
    {
        if (PowerLogSyntax.IsGameStateGame(line.SourceMethod))
        {
            if (PowerLogSyntax.TryParseDebugPrintGamePlayer(line.Payload, out var playerId, out var name))
                _playerNames[playerId] = name;
            return;
        }

        if (!PowerLogSyntax.IsGameStatePower(line.SourceMethod) && line.SourceMethod is not null)
            return;

        // Bare payloads (unit tests / snippets without the D-timestamp prefix) still apply.
        if (line.SourceMethod is null && string.IsNullOrWhiteSpace(line.Payload))
            return;

        switch (line.Kind)
        {
            case PowerLogLineKind.CreateGame:
                BeginGame();
                break;
            case PowerLogLineKind.GameEntity:
                AttachGameEntity(line.Payload);
                break;
            case PowerLogLineKind.PlayerEntity:
                AttachPlayer(line.Payload);
                break;
            case PowerLogLineKind.FullEntity:
                BeginFullEntity(line.Payload);
                break;
            case PowerLogLineKind.ShowEntity:
                BeginShowEntity(line.Payload);
                break;
            case PowerLogLineKind.HideEntity:
                ApplyHideEntity(line.Payload);
                break;
            case PowerLogLineKind.TagChange:
                ApplyTagChange(line.Payload);
                break;
            case PowerLogLineKind.TagValue:
                ApplyPendingTag(line.Payload);
                break;
        }
    }

    private void BeginGame()
    {
        _entities.Clear();
        _pending = null;
        _events.Clear();
        _playerNames.Clear();
        State.ResetMatch();
        Record(GameEventKind.GameCreated, 1, null, 0, null, null, "CREATE_GAME");
    }

    private void AttachGameEntity(string payload)
    {
        if (!PowerLogSyntax.TryParseGameEntityHeader(payload, out var entityId))
            return;
        _pending = GetOrCreate(entityId);
        _pending.CardType = "GAME";
        _pending.Zone = "PLAY";
    }

    private void AttachPlayer(string payload)
    {
        if (!PowerLogSyntax.TryParsePlayerHeader(payload, out var entityId, out var playerId))
            return;
        var entity = GetOrCreate(entityId);
        entity.CardType = "PLAYER";
        entity.PlayerId = playerId;
        entity.Controller = playerId;
        entity.Zone = "PLAY";
        _pending = entity;
    }

    private void BeginFullEntity(string payload)
    {
        if (!PowerLogSyntax.TryParseCreateEntity(payload, out var entityId, out var cardId))
        {
            _pending = null;
            return;
        }

        var entity = GetOrCreate(entityId);
        ApplyEntityHints(entity, payload);
        if (cardId is not null)
            entity.CardId = cardId;
        _pending = entity;
    }

    private void BeginShowEntity(string payload)
    {
        if (!PowerLogSyntax.TryParseShowEntity(payload, out var entityId, out var cardId))
        {
            _pending = null;
            return;
        }

        var entity = GetOrCreate(entityId);
        ApplyEntityHints(entity, payload);
        // SHOW_ENTITY is the client's local reveal of a previously hidden card.
        if (cardId is not null)
            RevealCard(entity, cardId);
        _pending = entity;
    }

    private void ApplyHideEntity(string payload)
    {
        _pending = null;
        if (!PowerLogSyntax.TryParseHideEntity(payload, out var entityId, out var tag, out var value))
            return;
        ApplyTag(GetOrCreate(entityId), tag, value);
    }

    private void ApplyTagChange(string payload)
    {
        _pending = null;
        if (!PowerLogSyntax.TryParseTagChange(payload, out var entityId, out var tag, out var value))
            return;
        ApplyTag(GetOrCreate(entityId), tag, value);
    }

    private void ApplyPendingTag(string payload)
    {
        if (_pending is null)
            return;
        if (!PowerLogSyntax.TryParseTagValue(payload, out var tag, out var value))
            return;
        ApplyTag(_pending, tag, value);
    }

    private void ApplyTag(EntityRecord entity, string tag, string value)
    {
        switch (tag.ToUpperInvariant())
        {
            case "ZONE":
                MoveZone(entity, PowerLogSyntax.NormalizeZone(value));
                break;
            case "CONTROLLER":
                if (PowerLogSyntax.TryParseInt(value, out var controller))
                    entity.Controller = controller;
                break;
            case "PLAYER_ID":
                if (PowerLogSyntax.TryParseInt(value, out var playerId))
                    entity.PlayerId = playerId;
                break;
            case "CARDTYPE":
                entity.CardType = PowerLogSyntax.NormalizeCardType(value);
                break;
            case "ENTITY_ID":
                break;
            case "STATE" when entity.Id == 1:
                ApplyGameState(value);
                break;
            case "TURN" when entity.Id == 1:
                if (PowerLogSyntax.TryParseInt(value, out var turn))
                    State.Turn = turn;
                break;
        }
    }

    private void ApplyGameState(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized == "RUNNING")
            State.IsInGame = true;
        else if (normalized is "COMPLETE" or "FINAL_GAMEOVER")
        {
            State.IsInGame = false;
            Record(GameEventKind.GameEnded, 1, null, 0, null, null, "Game STATE=" + normalized);
        }
    }

    private void RevealCard(EntityRecord entity, string cardId)
    {
        var wasUnknownInFriendlyDeck = entity.CountedInFriendlyDeck
            && string.IsNullOrEmpty(entity.CardId)
            && State.FriendlyController is int friendly
            && entity.Controller == friendly;

        entity.CardId = cardId;

        if (wasUnknownInFriendlyDeck)
            State.FriendlyDeck.PromoteUnknown(cardId);

        InferFriendlyFromDeckReveal(entity);

        if (IsOpponentController(entity.Controller) && entity.IsTrackableCard && !entity.RecordedOpponentSeen)
        {
            entity.RecordedOpponentSeen = true;
            State.OpponentSeen.Add(cardId);
            Record(
                GameEventKind.Reveal,
                entity.Id,
                cardId,
                entity.Controller,
                entity.Zone,
                entity.Zone,
                $"P{entity.Controller} revealed {cardId}");
        }
    }

    private void InferFriendlyFromDeckReveal(EntityRecord entity)
    {
        // The dispatcher only SHOW_ENTITYs deck cards to the local client.
        if (State.FriendlyController.HasValue)
            return;
        if (entity.Controller <= 0)
            return;
        if (!string.Equals(entity.Zone, "DECK", StringComparison.Ordinal))
            return;

        State.FriendlyController = entity.Controller;
        State.OpponentController = entity.Controller == 1 ? 2 : 1;
        RecountFriendlyDeckAfterControllerKnown();
    }

    private void RecountFriendlyDeckAfterControllerKnown()
    {
        if (State.FriendlyController is not int friendly)
            return;

        State.FriendlyDeck.ReplaceAll([]);
        foreach (var entity in _entities.Values)
        {
            entity.CountedInFriendlyDeck = false;
            if (entity.Controller == friendly && entity.IsInDeck && entity.IsTrackableCard)
                CountInFriendlyDeck(entity);
        }
    }

    private void MoveZone(EntityRecord entity, string newZone)
    {
        var oldZone = entity.Zone;
        if (string.Equals(oldZone, newZone, StringComparison.Ordinal))
        {
            EnsureDeckAccounting(entity);
            return;
        }

        entity.Zone = newZone;

        if (string.Equals(oldZone, "DECK", StringComparison.Ordinal)
            && string.Equals(newZone, "HAND", StringComparison.Ordinal))
        {
            OnDraw(entity, oldZone, newZone);
            return;
        }

        if (string.Equals(oldZone, "HAND", StringComparison.Ordinal)
            && string.Equals(newZone, "PLAY", StringComparison.Ordinal))
        {
            OnPlay(entity, oldZone, newZone);
            return;
        }

        if (string.Equals(oldZone, "DECK", StringComparison.Ordinal))
            LeaveFriendlyDeck(entity);

        if (string.Equals(oldZone, "HAND", StringComparison.Ordinal))
            State.RemoveFromFriendlyHand(entity.Id);

        if (string.Equals(newZone, "DECK", StringComparison.Ordinal))
            EnsureDeckAccounting(entity);

        if (string.Equals(newZone, "HAND", StringComparison.Ordinal)
            && IsFriendlyController(entity.Controller)
            && entity.IsTrackableCard)
        {
            State.AddToFriendlyHand(entity.ToTrackedCard());
        }

        // Skip the CREATE_GAME / FULL_ENTITY seating flood (INVALID → DECK/PLAY/…).
        if (!string.Equals(oldZone, "INVALID", StringComparison.Ordinal))
        {
            Record(
                GameEventKind.ZoneChange,
                entity.Id,
                entity.CardId,
                entity.Controller,
                oldZone,
                newZone,
                $"P{entity.Controller} {Label(entity)} {oldZone}→{newZone}");
        }
    }

    private void OnDraw(EntityRecord entity, string from, string to)
    {
        InferFriendlyFromDeckReveal(entity);
        LeaveFriendlyDeck(entity);

        if (IsFriendlyController(entity.Controller) && entity.IsTrackableCard)
            State.AddToFriendlyHand(entity.ToTrackedCard());

        Record(
            GameEventKind.Draw,
            entity.Id,
            entity.CardId,
            entity.Controller,
            from,
            to,
            $"P{entity.Controller} drew {Label(entity)}");
    }

    private void OnPlay(EntityRecord entity, string from, string to)
    {
        State.RemoveFromFriendlyHand(entity.Id);
        if (entity.IsTrackableCard && !entity.RecordedPlay)
        {
            entity.RecordedPlay = true;
            State.AddPlayed(entity.ToTrackedCard());
        }

        if (IsOpponentController(entity.Controller)
            && entity.CardId is not null
            && entity.IsTrackableCard
            && !entity.RecordedOpponentSeen)
        {
            entity.RecordedOpponentSeen = true;
            State.OpponentSeen.Add(entity.CardId);
        }

        Record(
            GameEventKind.Play,
            entity.Id,
            entity.CardId,
            entity.Controller,
            from,
            to,
            $"P{entity.Controller} played {Label(entity)}");
    }

    private void EnsureDeckAccounting(EntityRecord entity)
    {
        if (!entity.IsInDeck || !entity.IsTrackableCard)
            return;
        if (State.FriendlyController is null)
            return;
        if (!IsFriendlyController(entity.Controller))
            return;
        if (entity.CountedInFriendlyDeck)
            return;
        CountInFriendlyDeck(entity);
    }

    private void CountInFriendlyDeck(EntityRecord entity)
    {
        entity.CountedInFriendlyDeck = true;
        if (string.IsNullOrEmpty(entity.CardId))
            State.FriendlyDeck.AddUnknown();
        else
            State.FriendlyDeck.Add(entity.CardId);
    }

    private void LeaveFriendlyDeck(EntityRecord entity)
    {
        if (!entity.CountedInFriendlyDeck)
            return;
        entity.CountedInFriendlyDeck = false;
        State.FriendlyDeck.RemoveOne(entity.CardId);
    }

    private void ApplyEntityHints(EntityRecord entity, string payload)
    {
        if (entity.Controller == 0
            && PowerLogSyntax.TryReadAssignedValue(payload, "player", out var playerText)
            && PowerLogSyntax.TryParseInt(playerText, out var player)
            && player > 0)
        {
            entity.Controller = player;
        }

        if (string.Equals(entity.Zone, "INVALID", StringComparison.Ordinal)
            && PowerLogSyntax.TryReadAssignedValue(payload, "zone", out var zoneText))
        {
            entity.Zone = PowerLogSyntax.NormalizeZone(zoneText);
        }
    }

    private EntityRecord GetOrCreate(int entityId)
    {
        if (_entities.TryGetValue(entityId, out var existing))
            return existing;

        var created = new EntityRecord(entityId);
        _entities[entityId] = created;
        return created;
    }

    private bool IsFriendlyController(int controller) =>
        State.FriendlyController is int friendly && controller == friendly;

    private bool IsOpponentController(int controller) =>
        controller > 0 && State.FriendlyController is int friendly && controller != friendly;

    private void Record(
        GameEventKind kind,
        int entityId,
        string? cardId,
        int controller,
        string? from,
        string? to,
        string summary)
    {
        _events.Add(new GameEvent(kind, entityId, cardId, controller, from, to, summary));
        if (_events.Count > _eventCapacity)
            _events.RemoveRange(0, _events.Count - _eventCapacity);
    }

    private static string Label(EntityRecord entity) => entity.CardId ?? $"entity {entity.Id}";

    private sealed class EntityRecord(int id)
    {
        public int Id { get; } = id;
        public string? CardId { get; set; }
        public int Controller { get; set; }
        public int PlayerId { get; set; }
        public string Zone { get; set; } = "INVALID";
        public string CardType { get; set; } = string.Empty;
        public bool CountedInFriendlyDeck { get; set; }
        public bool RecordedPlay { get; set; }
        public bool RecordedOpponentSeen { get; set; }

        public bool IsInDeck => string.Equals(Zone, "DECK", StringComparison.Ordinal);

        public bool IsTrackableCard =>
            CardType is "" or "MINION" or "SPELL" or "WEAPON" or "HERO" or "INVALID" or "LOCATION"
            && CardType is not ("GAME" or "PLAYER" or "HERO_POWER" or "ENCHANTMENT");

        public TrackedCard ToTrackedCard() => new(Id, CardId, Controller);
    }
}
