# Hearthstone Deck Tracker

Windows-first desktop 《炉石传说》 deck/card tracker. This repository is a **skeleton**: project layout, read-only `Power.log` ingest stubs, and a WPF overlay/sidebar shell. It does not track games yet.

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
└── src/HearthstoneDeckTracker.Core    net8.0 class library (log ingest + game-state stubs)
tests/HearthstoneDeckTracker.Core.Tests   xUnit smoke tests (parser / log.config / tail reader)
```

| Piece | Role now | Not in this skeleton |
| --- | --- | --- |
| `LogConfigWriter` | Merge `[Power]` / `[LoadingScreen]` (`LogLevel=1`, `FilePrinting=True`, `Verbose=True` on Power) | Other log channels |
| `PowerLogTailReader` | `FileStream` + `FileShare.ReadWrite` line tail | Install-path discovery, persisted offset |
| `PowerLogLineParser` | Classify `TAG_CHANGE` / `SHOW_ENTITY` / `FULL_ENTITY` / other | Full Power.log state machine |
| `GameState` / `DeckRemaining` | In-memory stubs | Draw/play math, secrets, fatigue |
| `MainWindow` | Status, paths, **Ensure log.config** | Live match UI |
| `OverlayWindow` | `AllowsTransparency` + `Topmost` | Click-through (`WS_EX_TRANSPARENT`), no-activate, HS window tracking |
| Card data | — | HearthstoneJSON / HearthDb |
| Shipping | — | Velopack |

Deck **code** parsing is not required here. A sibling reusable service lives in [YinshawnRao/hearthstone-decks-mcp](https://github.com/YinshawnRao/hearthstone-decks-mcp) and is optional for later wiring.

## Build

Requires the **.NET 8 SDK**. The WPF app targets `net8.0-windows` with `UseWPF=true` and `PlatformTarget=x64`. **Running the GUI requires Windows.** `Directory.Build.props` sets `EnableWindowsTargeting` so `dotnet build` of the WPF project can succeed on Linux agents; you still cannot launch the overlay/sidebar there. Linux/macOS CI should treat Core + tests as the portable slice.

```bash
# Library + smoke tests (works on Linux/macOS/Windows)
dotnet test tests/HearthstoneDeckTracker.Core.Tests/HearthstoneDeckTracker.Core.Tests.csproj

# Full WPF app — run this on Windows
dotnet build HearthstoneDeckTracker.sln -c Release
dotnet run --project src/HearthstoneDeckTracker.App/HearthstoneDeckTracker.App.csproj
```

After **Ensure log.config**, restart Hearthstone so it creates `Logs\Power.log` under the game install directory.

## References

- [Setting up the log.config (HearthSim HDT wiki)](https://github.com/HearthSim/Hearthstone-Deck-Tracker/wiki/Setting-up-the-log.config)
- [HearthSim python-hslog — Power.log packets](https://github.com/HearthSim/python-hslog)
- [Hearthstone Game State Protocol](https://hearthsim.info/docs/gamestate-protocol/)
- [HearthstoneJSON](https://hearthstonejson.com/) / [HearthDb](https://github.com/HearthSim/HearthDb)

## Status

骨架搭建中. Out of scope for this drop: full Power.log state machine, card database, window tracking, Velopack, CN-server research.
