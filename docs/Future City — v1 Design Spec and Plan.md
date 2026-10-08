# Future City — v1 Design Spec and Plan

Oct 8, 2026 · @Ljubomir Bacovic

v1 is built in 10 phases, each ending in a playable or testable build. The simulation is a standalone .NET library and Godot (C#) only renders and takes input, so most work can be built and tested headless with Claude Code.

## Architecture

The simulation owns all game state and rules; Godot only draws it and sends player commands. This keeps the game deterministic, testable without the engine, and ready for multiplayer in v2.

| Project | Responsibility | Depends on |
| --- | --- | --- |
| `FutureCity.Sim` | World state, rules, economy, AI, emergence engine | .NET only |
| `FutureCity.Content` | Data definitions: resources, buildings, units, techs, institutions, civilizations (JSON) | — |
| `FutureCity.Sim.Tests` | xUnit tests and headless scenario runs | Sim, Content |
| `FutureCity.Headless` | Console runner: simulate many games for balancing and regression | Sim, Content |
| `FutureCity.Game` | Godot project: rendering, input, UI, audio | Sim, Content |

**Core rules**

- **Fixed tick:** the simulation advances in fixed ticks (e.g. 10 per second at 1×). Rendering interpolates between ticks.
- **Commands only:** the player and the AI change the world only through command objects (`MoveUnits`, `Build`, `SetTax` …) queued for the next tick.
- **Determinism:** seeded RNG owned by the simulation, no wall-clock time, stable iteration order, fixed-point or integer math for anything affecting state. The same seed and commands always produce the same game.
- **ECS:** entities and components via Friflo.Engine.ECS; systems run in a fixed order each tick.
- **Emergence engine:** every tech, institution and era has a precondition expression evaluated against world state; the UI shows unmet conditions.
- **Save/load:** serialize the full world state; replays are seed + command log.
- **`CLAUDE.md`** documents these rules so every Claude Code session follows them.

## Phase 0 — Foundation

**Goal:** an empty but correctly structured game that ticks, renders a map and saves.

- [x] Create the solution and the five projects; reference Sim from the Godot project
- [x] Configure VS Code: C# Dev Kit, `launch.json` for Godot, build tasks
- [x] Write `CLAUDE.md` with architecture and determinism rules
- [x] Set up Git, `.gitignore` for Godot and .NET, CI (GitHub Actions: build + tests)
- [x] Implement the tick loop, command queue and seeded RNG
- [x] Choose and integrate the ECS library (Friflo.Engine.ECS)
- [x] Content loader for JSON definitions with validation
- [x] Save/load and a determinism test (same seed + commands = identical state hash)
- [x] Headless runner that simulates N ticks and prints a state summary
- [x] Godot: isometric tile map, RTS camera (pan, zoom, edge scroll), pause and speed controls

**Exit:** an empty map renders, time runs and pauses, a save reloads identically, CI is green.

## Phase 1 — Primitive survival loop

**Goal:** a band of villagers survives by hunting and gathering.

- [x] Small map generator: terrain, water, forests, berry bushes, game animals (seeded)
- [x] Citizen agents: age, health, hunger, simple needs
- [x] Pathfinding: grid A\* for individuals
- [x] Orders: move, gather, hunt, return to camp
- [x] Food stores, eating, starvation, natural death
- [x] Population growth from food surplus and shelter
- [x] Renewable resources: animals breed, plants regrow; overhunting depletes them
- [x] Godot: unit sprites, box selection, right-click orders, resource top bar
- [x] Placeholder art (simple shapes) so gameplay is not blocked by art

**Exit:** a 5-minute session where good choices grow the band and poor ones lead to famine.

## Phase 2 — Settlement and production

**Goal:** the band settles, farms and enters the Dark Ages through the emergence engine.

- [x] Wood, stone and clay gathering
- [x] Building placement and construction: huts, storage, farms, workshops
- [x] Farming with soil fertility and seasons
- [x] Production chains: grain → flour → bread, wood → tools
- [x] Logistics: goods carried by citizens; distance affects throughput
- [x] Jobs: citizens assigned to buildings; automatic assignment by demand
- [x] Emergence engine: precondition expressions, evaluation each N ticks, "what's missing" data for the UI
- [x] First techs and institutions: tools, farming, chiefdom
- [x] Era transition Primitive → Dark Ages
- [x] Godot: building placement UI, construction states, era banner, research panel

**Exit:** a player reaches the Dark Ages in about 5–7 minutes, and headless runs show stable population growth.

## Phase 3 — Economy

**Goal:** prices and money emerge from the simulation instead of being set by the designer.

- [x] Household and workshop inventories; shared stores replaced by ownership
- [x] Barter between households and settlements based on surplus and need
- [x] Mint and coinage; coin quality setting; money supply tracking
- [x] Marketplace: buy and sell orders, price discovery from supply and demand
- [x] Wages and job choice driven by prices
- [x] Taxes and tariffs set by the player; treasury
- [x] Guilds that regulate crafts (quality up, competition down)
- [x] Social classes and happiness (needs met, taxes, safety)
- [x] Inflation from coin debasement
- [x] Godot: markets and prices dashboard, treasury panel, tax sliders
- [x] Headless tests: prices converge after shocks; debasement raises prices

**Exit:** headless runs show emergent, stable prices, and debasing coins visibly causes inflation.

## Phase 4 — Military and conflict

**Goal:** armies cost the economy something, and war has economic consequences.

- [ ] Unit definitions: clubmen, spearmen, archers, cavalry, rams, boats
- [ ] Recruiting removes workers; units need food, equipment and (later) pay
- [ ] Levies vs mercenaries depending on available money
- [ ] Combat: attack, damage, range, armour, morale
- [ ] Group movement with flow fields and simple formations
- [ ] Fortifications: palisades, stone walls, gates, towers
- [ ] Destruction, loot and trade disruption
- [ ] Diplomacy states: peace, alliance, trade agreement, tribute, war
- [ ] Godot: combat visuals, health bars, rally points, diplomacy panel

**Exit:** two human-controlled test civilizations can fight a war, and the loser's economy visibly suffers.

## Phase 5 — Rival AI

**Goal:** 1–3 AI civilizations that play the full loop using the same commands as the player.

- [ ] AI controller that issues commands only (no direct state access beyond what a player can see)
- [ ] Economy planner: gathering, building order, job balance
- [ ] Military planner: defence, raids, attack timing
- [ ] Diplomacy logic: trade, alliances, war declarations
- [ ] Personalities: trader, conqueror, builder
- [ ] Difficulty levels as resource bonuses, never rule-breaking
- [ ] Headless AI-vs-AI tournaments for tuning

**Exit:** AI civilizations reach the Medieval era on their own in headless runs, and a new player loses to the hard AI.

## Phase 6 — Medieval era, crises and victory

**Goal:** a complete 15–20 minute game from start to victory or defeat.

- [ ] Medieval institutions: written law, monasteries, guild charters, town charter
- [ ] Era transition Dark Ages → Medieval
- [ ] Crises: famine, epidemics (density and water), raids, unrest
- [ ] Crisis explainer pop-ups (what happened, why, historical parallel)
- [ ] Victory conditions: chartered market town, prosperity score at 20 minutes, conquest; sandbox mode
- [ ] Defeat detection and end-of-game summary screen with charts
- [ ] Game setup screen: civilization, map size, AI count, difficulty, seed

**Exit:** full games against AI finish in 15–20 minutes with all three victory types reachable.

## Phase 7 — Civilizations and balancing

**Goal:** three distinct civilizations that are fair to play against each other.

- [ ] Venetians: coastal start, fish and timber, trade and ship bonuses
- [ ] Franks: fertile plains, farming bonus, heavy cavalry
- [ ] Mongols: steppe start, herding, horse archers, weaker towns
- [ ] Map generator biased by civilization start type
- [ ] Headless balance runs: thousands of AI-vs-AI games per matchup; win rates and time-to-era reports
- [ ] Tune content data until every matchup is within 45–55% win rate
- [ ] Performance check at the 100-citizen cap with 4 civilizations

**Exit:** balance report within targets and stable frame rate on a mid-range PC.

## Phase 8 — Codex, tutorials, UI and art

**Goal:** the game teaches and looks finished.

- [ ] Codex: an entry per resource, building, unit, tech, institution and crisis, with a short text and a "go deeper" layer
- [ ] "Your history vs real history" comparisons at key moments
- [ ] Tutorial scenarios: surviving a famine, why debasing coins causes inflation, defending against a raid
- [ ] Notifications linked to codex entries
- [ ] Overlays: fertility, resources, happiness, health, trade routes
- [ ] Final illustrated 2D art: terrain, buildings per era, units, UI skin, icons
- [ ] Music and sound effects
- [ ] Settings: resolution, audio, key bindings, UI scale

**Exit:** a first-time player finishes the tutorials and a full game without outside help.

## Phase 9 — Steam release

**Goal:** Future City - Origins (v1) is live on Steam for Windows, free to play.

- [ ] Steamworks account, app ID and SDK integration (achievements, cloud saves)
- [ ] Store page: capsule art, screenshots, trailer, description
- [x] Decide the business model: free at launch, premium offering later
- [ ] Closed playtest via Steam Playtest; collect feedback and crash reports
- [ ] Fix and polish pass from playtest findings
- [ ] Windows export pipeline in CI
- [ ] Launch as Early Access or full release

**Exit:** the build is approved by Steam and released.
