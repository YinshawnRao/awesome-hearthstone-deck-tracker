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
- Downloads HearthstoneJSON card metadata and rendered art into `%LOCALAPPDATA%\AwesomeHearthstoneDeckTracker\cache\` (see below)

It does **not** include HearthMirror, memory readers, DLL injection, packet sniffing, Overwolf, or copies of [HearthSim/Hearthstone-Deck-Tracker](https://github.com/HearthSim/Hearthstone-Deck-Tracker) source.

## Architecture

```
HearthstoneDeckTracker.sln
├── src/HearthstoneDeckTracker.App     WPF net8.0-windows (MainWindow + OverlayWindow stub)
└── src/HearthstoneDeckTracker.Core    net8.0 (Power.log ingest + HearthstoneJSON catalog)
tests/HearthstoneDeckTracker.Core.Tests   xUnit + Fixtures/*.log + cards.zhCN.subset.json
```

| Piece | Role now | Not in this spike |
| --- | --- | --- |
| `LogConfigWriter` | Merge `[Power]` / `[LoadingScreen]` (`LogLevel=1`, `FilePrinting=True`, `Verbose=True` on Power) | Other log channels |
| `PowerLogTailReader` | `FileStream` + `FileShare.ReadWrite` line tail | Persisted offset, last-`CREATE_GAME` seek |
| `PowerLogLineParser` | Classify `CREATE_GAME` / `TAG_CHANGE` / `SHOW_ENTITY` / `FULL_ENTITY` / `tag=` | Full tokenizer for META_DATA / options |
| `PowerLogGameStateBuilder` | Entity map + DECK↔HAND↔PLAY↔GRAVEYARD | Secrets, fatigue, discover, Battlegrounds |
| `GameState` / `DeckRemaining` | Friendly remaining, hand, plays, opponent seen | dbfId-only APIs |
| `HearthstoneJsonCatalog` | zhCN names / cost / rarity / class / type, 24h disk cache | HearthDb, live memory |
| `CardArtCache` | Download 256x render PNGs, keyed by CardID | Overlay polish, 512x gallery |
| `MainWindow` | 中文名 + 费用 + 缩略图 lists, catalog status, Tail | Live overlay binding |
| `OverlayWindow` | `AllowsTransparency` + `Topmost` | Click-through, HS window tracking |
| Shipping | — | Velopack |

Deck **code** parsing is not required here. A sibling reusable service lives in [YinshawnRao/hearthstone-decks-mcp](https://github.com/YinshawnRao/hearthstone-decks-mcp) (same HSJSON URL + art template). This repo implements the catalog in C# and does **not** spawn Node.

## Power.log spike

The builder consumes **`GameState.DebugPrintPower`** and **`GameState.DebugPrintGame`** only. `PowerTaskList.*` is ignored so the same packets are not applied twice.

Handled packets (HearthSim [game-state protocol](https://hearthsim.info/docs/gamestate-protocol/)):

- `CREATE_GAME` + nested `GameEntity` / `Player` + indented `tag=` bodies
- `FULL_ENTITY` (deck seating; empty `CardID` counts as unknown)
- `SHOW_ENTITY` (local reveal of a hidden card)
- `TAG_CHANGE` for `ZONE`, `STATE`, `TURN`, `CONTROLLER`

**Draw** = DECK→HAND. **Play** = HAND→PLAY. Opponent cards enter `OpponentSeen` when `SHOW_ENTITY` supplies a `CardID`. The friendly `CONTROLLER` is inferred from the first deck-card `SHOW_ENTITY` (the client only receives those for its own draws).

### Limits

- No secret helper, no mulligan UI, no overlay positioning
- No CN-server / install-path research beyond a couple of default Windows guess paths
- Large live `Power.log` files are replayed from the start when Tail is checked (no “seek last CREATE_GAME” yet)
- Enchantments, SETASIDE/SECRET, Discover, The Coin edge cases, and spectator logs are out of scope
- Still **no** memory reading, injection, or packet sniffing

## Card names and art (HearthstoneJSON)

MainWindow lists (套牌剩余 / 手牌 / 已打出 / 对手可见) show **中文名** and mana cost. The Power.log `CardID` (e.g. `CS2_029`) stays on the tooltip. Unknown IDs render as the raw string and do not crash.

On startup (and again on first Tail if the catalog is still empty) the app loads:

`https://api.hearthstonejson.com/v1/latest/zhCN/cards.json`

Full `cards.json` is used (not collectible-only) so tokens and other non-collectible IDs that appear in logs still resolve. The JSON is cached at:

`%LOCALAPPDATA%\AwesomeHearthstoneDeckTracker\cache\cards.zhCN.json`

TTL is 24 hours. If the network fetch fails, the last good file is used and the status line shows **离线缓存**. First run needs network; later launches work offline until the cache is deleted.

Card art is **downloaded once** and stored under:

`%LOCALAPPDATA%\AwesomeHearthstoneDeckTracker\cache\art\zhCN\256x\{CARD_ID}.png`

from `https://art.hearthstonejson.com/v1/render/latest/zhCN/256x/{CARD_ID}.png`. The UI reads the local file; it does not hotlink art for every redraw.

**IP note:** Card names and rendered art are Blizzard / Hearthstone intellectual property, delivered via [HearthstoneJSON](https://hearthstonejson.com/). This cache is for **personal local display in this unofficial tracker only**. Do not redistribute the downloaded JSON or PNG files.

Catalog status in the window:

- `卡表加载中` — fetch / disk read in progress
- `已加载 N 张` — catalog ready (network or fresh cache)
- `离线缓存` — fetch failed, stale file reused

### How to run tests

```bash
dotnet test tests/HearthstoneDeckTracker.Core.Tests/HearthstoneDeckTracker.Core.Tests.csproj
```

Fixtures live in `tests/HearthstoneDeckTracker.Core.Tests/Fixtures/` (`minimal_draw_play.log`, `opponent_reveal.log`, `cards.zhCN.subset.json`). Log snippets are synthetic but use real opcode / entity-bracket shapes. Catalog tests parse the subset fixture and mock HTTP — they do **not** require live network.

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

Power.log draw/play spike plus zhCN HearthstoneJSON names/art cache. Out of scope: full secret helper, mulligan UI, overlay positioning, CN-server research, Velopack, HearthMirror.
