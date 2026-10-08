# Future City

A historical strategy and city-simulation game where a civilization grows from hunter-gatherers into a complex economy, and where every technology and institution emerges from real-world causes.

> **Status:** early development. v1, **Future City - Origins**, is in progress and will be free on Steam.

## About

Future City combines real-time strategy in the spirit of *Age of Empires II* with a deep economic simulation. You start with a handful of villagers hunting and gathering. Surplus leads to farming, farming to towns, towns to money, markets and armies.

Nothing unlocks on a timer. Markets appear when trade outgrows barter; money appears when someone can guarantee coin value; armies need food, equipment and pay. The game is designed to be **educational for ages 14+ and adults**: accurate about how societies actually developed, explained through an in-game codex.

### v1 features (Future City - Origins)

- **Three eras:** Primitive, Dark Ages, Medieval
- **Short games:** 15–20 minutes, pausable real time
- **Rival AI:** 1–3 AI civilizations that play by the same rules as you
- **Three civilizations:** Venetians, Franks, Mongols, each with different starting conditions
- **Emergent economy:** barter, coinage, markets, taxes, guilds, inflation, with prices from supply and demand
- **Warfare:** levies, mercenaries, fortifications, diplomacy
- **Crises:** famine, epidemics, raids, unrest, each with an explanation of what happened and why
- **Codex and tutorials** that teach the principles behind the game

Later versions extend the game through the Renaissance, industrial and modern eras (banking, stock exchanges, central banks) and into projected futures, plus multiplayer.

## Tech stack

- **Engine:** [Godot 4.7](https://godotengine.org) (.NET build), C#
- **Simulation:** standalone .NET 8 library, deterministic, fixed-tick, ECS-based ([Friflo.Engine.ECS](https://github.com/friflo/Friflo.Engine.ECS))
- **Tests:** xUnit, headless simulation runs
- **Platform:** Windows (Steam)

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) and the .NET 8 runtime (projects target `net8.0`)
- [Godot 4.7.2 .NET build](https://godotengine.org/download)
- VS Code with the C# Dev Kit extension (recommended)
- VS Code **Play** uses the `godotTools.editorPath.godot4` setting in `.vscode/settings.json`; change it if Godot is installed elsewhere. Command-line examples use a `GODOT` environment variable pointing to the same executable.

### Build and run

```bash
git clone https://github.com/<your-account>/future-city.git
cd future-city
dotnet build FutureCity.sln
dotnet test
```

Open `src/FutureCity.Game/project.godot` in the Godot editor and press **F5**, or run:

```bash
<path-to-godot> --path src/FutureCity.Game
```

Controls:

- **Left-click** a villager to select, **Shift**-click to add, **drag** a box to select several, **Esc** to clear. Click a building to inspect it.
- **Right-click** with villagers selected: on a deer to hunt; on a berry bush, carcass, stone outcrop or clay pit to gather; on a forest to cut wood; on a construction site to build; on a farm, workshop or shrine to work there; on the camp to return; anywhere else to move.
- **Build** menu (bottom right): pick a building, then left-click to place it (**Shift** to place several), right-click or **Esc** to cancel. Buttons explain what is still missing.
- **R** (or the era button) opens discoveries: the next era's checklist, technologies with their progress and a research focus, and institutions you can establish.
- **E** (or the Economy button, once families own their goods) opens the economy: market prices and their history, the price index and money supply, the treasury, and sliders for tribute, market tax, tariff and the silver content of new coins.
- **Space** pause, **1–4** speed, **WASD**/arrows/screen edge/middle-drag to pan, mouse wheel to zoom.
- **F5** quick save, **F9** quick load. The game also autosaves every 2 minutes of game time.

Launch options for testing go after `--`: `--seed=N`, `--map=small|medium|large`, `--zoom=F`, `--autoplay` (a stand-in computer player settles and builds; `--autoplay=forage` only forages), `--skip=N` (simulate N ticks first), `--select-all`, `--research` (open discoveries), `--economy` (open the economy panel), `--place=ID` (start placing a building), `--select-building=ID`, `--screenshot=PATH --frames=N`.

### Headless simulation

Run a full game without graphics, useful for balancing and testing. By default a stand-in player settles, builds, farms and researches (`--player settle`); `--player forage` only forages and `--player idle` gives no orders. The output shows population, food, births, deaths, buildings, goods, discoveries, when the next era was reached, when Private property, Coinage and Guilds were established, the money supply, the price index and a few prices. Experiments: `--debase-at <s> --quality <n>` debases the coinage at a game second, `--shock-at <s>` destroys half the grain:

```bash
dotnet run --project src/FutureCity.Headless -- --seed 42 --ticks 12000
dotnet run --project src/FutureCity.Headless -- --seed 42 --ticks 3000 --timeline   # band state every game minute
dotnet run --project src/FutureCity.Headless -- --games 20 --map medium   # many seeds
dotnet run --project src/FutureCity.Headless -- --player idle --ticks 3000   # no orders: watch a famine
dotnet run --project src/FutureCity.Headless -- --games 8 --ticks 12000 --debase-at 720 --quality 50   # inflation
dotnet run --project src/FutureCity.Headless -- --help
```

## Project structure

| Path | Purpose |
| --- | --- |
| `src/FutureCity.Sim` | Simulation: world state, rules, economy, AI |
| `src/FutureCity.Content` | Game data (JSON): resources, buildings, units, techs, civilizations |
| `src/FutureCity.Headless` | Console runner for headless games |
| `src/FutureCity.Game` | Godot project: rendering, input, UI, audio |
| `tests/FutureCity.Sim.Tests` | Unit and scenario tests |
| `docs/` | Design spec and development plan |

## Roadmap (v1)

0. Foundation: solution, tick loop, save/load, camera
1. Primitive survival loop
2. Settlement and production
3. Economy
4. Military and conflict
5. Rival AI
6. Medieval era, crises and victory
7. Civilizations and balancing
8. Codex, tutorials, UI and art
9. Steam release

## AI-assisted development

This project is developed with AI coding agents. See [AGENTS.md](AGENTS.md) for project rules and [CLAUDE.md](CLAUDE.md) for Claude Code workflow.

## License

TBD
