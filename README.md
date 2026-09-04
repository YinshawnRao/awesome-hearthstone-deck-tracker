# Hearthstone Deck Tracker

Windows-first desktop 《炉石传说》 deck/card tracker. This repository has a WPF shell plus a **Power.log draw/play spike**: classified log lines are applied to `GameState` (friendly deck remaining, draws, plays, opponent reveals). It is not a full tracker.

The repo name has no `-win` suffix so later ports can share this tree. The first UI target is Windows (WPF).

## 原则

- 只读 `Power.log`，不读内存、不注入、不抓包
- 只显示玩家本可观察的信息（纸笔等价）
- 非官方第三方工具

## Compliance

This tool only:

- Writes `%LOCALAPPDATA%\Blizzard\Hearthstone\log.config` so the **game itself** prints `Power.log` / `LoadingScreen.log`
- Tail-reads those text files with `FileShare.ReadWrite`

It does **not** include HearthMirror, memory readers, DLL injection, packet sniffing, Overwolf, or copies of [HearthSim/Hearthstone-Deck-Tracker](https://github.com/HearthSim/Hearthstone-Deck-Tracker) source.

## Architecture

```
HearthstoneDeckTracker.sln
├── src/HearthstoneDeckTracker.App     WPF net8.0-windows (MainWindow sidebar + OverlayWindow stub)
└── src/HearthstoneDeckTracker.Core    net8.0 class library (log ingest + GameState builder)
tests/HearthstoneDeckTracker.Core.Tests   xUnit + Fixtures/*.log snippets
```

| Piece | Role now | Not in this spike |
| --- | --- | --- |
| `LogConfigWriter` | Merge `[Power]` / `[LoadingScreen]` (`LogLevel=1`, `FilePrinting=True`, `Verbose=True` on Power) | Other log channels |
| `PowerLogTailReader` | `FileStream` + `FileShare.ReadWrite` line tail | Persisted offset, last-`CREATE_GAME` seek |
| `PowerLogLineParser` | Classify `CREATE_GAME` / `TAG_CHANGE` / `SHOW_ENTITY` / `FULL_ENTITY` / `tag=` | Full tokenizer for META_DATA / options |
| `PowerLogGameStateBuilder` | Entity map + DECK↔HAND↔PLAY↔GRAVEYARD | Secrets, fatigue, discover, Battlegrounds |
| `GameState` / `DeckRemaining` | Friendly remaining, hand, plays, opponent seen | Card names / dbfIds |
| `MainWindow` | Status, **Tail Power.log**, last-N events | Live overlay binding |
| `OverlayWindow` | `AllowsTransparency` + `Topmost` | Click-through, HS window tracking |
| Card data | — | HearthstoneJSON / HearthDb |
| Shipping | — | Velopack |

Deck **code** parsing is not required here. A sibling reusable service lives in [YinshawnRao/hearthstone-decks-mcp](https://github.com/YinshawnRao/hearthstone-decks-mcp) and is optional for later wiring.

## Power.log spike

The builder consumes **`GameState.DebugPrintPower`** and **`GameState.DebugPrintGame`** only. `PowerTaskList.*` is ignored so the same packets are not applied twice.

Handled packets (HearthSim [game-state protocol](https://hearthsim.info/docs/gamestate-protocol/)):

- `CREATE_GAME` + nested `GameEntity` / `Player` + indented `tag=` bodies
- `FULL_ENTITY` (deck seating; empty `CardID` counts as unknown)
- `SHOW_ENTITY` (local reveal of a hidden card)
- `TAG_CHANGE` for `ZONE`, `STATE`, `TURN`, `CONTROLLER`

**Draw** = DECK→HAND. **Play** = HAND→PLAY. Opponent cards enter `OpponentSeen` when `SHOW_ENTITY` supplies a `CardID`. The friendly `CONTROLLER` is inferred from the first deck-card `SHOW_ENTITY` (the client only receives those for its own draws).

### Limits

- No secret helper, no mulligan UI, no card database, no overlay positioning
- No CN-server / install-path research beyond a couple of default Windows guess paths
- Large live `Power.log` files are replayed from the start when Tail is checked (no “seek last CREATE_GAME” yet)
- Enchantments, SETASIDE/SECRET, Discover, The Coin edge cases, and spectator logs are out of scope
- Still **no** memory reading, injection, or packet sniffing

### How to run tests

```bash
dotnet test tests/HearthstoneDeckTracker.Core.Tests/HearthstoneDeckTracker.Core.Tests.csproj
```

Fixtures live in `tests/HearthstoneDeckTracker.Core.Tests/Fixtures/` (`minimal_draw_play.log`, `opponent_reveal.log`). They are synthetic but use real opcode / entity-bracket shapes.

## Build

Requires the **.NET 8 SDK**. The WPF app targets `net8.0-windows` with `UseWPF=true` and `PlatformTarget=x64`. **Running the GUI requires Windows.** `Directory.Build.props` sets `EnableWindowsTargeting` so `dotnet build` of the WPF project can succeed on Linux agents; you still cannot launch the overlay/sidebar there. Linux/macOS CI should treat Core + tests as the portable slice.

```bash
# Library + tests (works on Linux/macOS/Windows)
dotnet test tests/HearthstoneDeckTracker.Core.Tests/HearthstoneDeckTracker.Core.Tests.csproj

# Full WPF app — run this on Windows
dotnet build HearthstoneDeckTracker.sln -c Release
dotnet run --project src/HearthstoneDeckTracker.App/HearthstoneDeckTracker.App.csproj
```

After **Ensure log.config**, restart Hearthstone so it creates `Logs\Power.log` under the game install directory. Paste that path into MainWindow and check **Tail Power.log**. If the path is missing or still the `<Hearthstone install>` placeholder, the window shows a status line and does not crash.

## References

- [Setting up the log.config (HearthSim HDT wiki)](https://github.com/HearthSim/Hearthstone-Deck-Tracker/wiki/Setting-up-the-log.config)
- [HearthSim python-hslog — Power.log packets](https://github.com/HearthSim/python-hslog)
- [Hearthstone Game State Protocol](https://hearthsim.info/docs/gamestate-protocol/)
- [HearthstoneJSON](https://hearthstonejson.com/) / [HearthDb](https://github.com/HearthSim/HearthDb)

## Status

Power.log draw/play spike. Out of scope: full secret helper, mulligan UI, card DB download, overlay positioning, CN-server research, Velopack.
