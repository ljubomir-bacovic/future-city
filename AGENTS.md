# AGENTS.md

Instructions for AI coding agents (Claude Code, GitHub Copilot, others) working in this repository.

## Project

**Future City** is a historical strategy and city-simulation game built with **Godot 4 (.NET / C#)**. v1 takes a civilization from hunter-gatherers to a medieval market town (Primitive, Dark Ages, Medieval eras) in 15–20 minute single-player games against rival AI. v1 ships as **Future City - Origins**, free, in English on Steam for Windows.

The game is educational: it must be accurate about **principles** (how economies, institutions, science and armies developed), not about exact dates and places. Technologies and institutions **emerge** when their real-world preconditions are met; nothing unlocks on a timer.

## Repository layout

```
FutureCity.sln
src/
  FutureCity.Sim/        # Simulation: world state, rules, economy, AI, emergence engine. Pure .NET.
  FutureCity.Content/    # Game data (JSON): resources, buildings, units, techs, institutions, civilizations
  FutureCity.Headless/   # Console runner: simulate many games for balancing and regression
  FutureCity.Game/       # Godot project (project.godot + .csproj): rendering, input, UI, audio
tests/
  FutureCity.Sim.Tests/  # xUnit tests and headless scenario tests
docs/                    # Design spec and plan
```

## Commands

```bash
dotnet build FutureCity.sln                      # build everything
dotnet test                                      # run all tests
dotnet run --project src/FutureCity.Headless -- --seed 42 --ticks 12000   # headless game (--help for options)
"$GODOT" --path src/FutureCity.Game              # run the game ($GODOT = path to Godot .NET executable)
"$GODOT" --headless --path src/FutureCity.Game --import   # import/validate the Godot project
"$GODOT" --path src/FutureCity.Game -- --seed=42 --zoom=0.35 --screenshot=shot.png --frames=60   # render N frames, save a PNG, quit
```

On this machine Godot is at `C:\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\` (use the `_console.exe` for command-line runs). `dotnet build` also builds the Godot project, so Godot runs the fresh assembly without its own build step.

Run `dotnet build` and `dotnet test` after every change. Work is not done while either fails. For visual changes, take a screenshot with the command above and look at it.

## Architecture rules (non-negotiable)

1. **The simulation owns all state.** `FutureCity.Sim` must never reference Godot (`using Godot;` is forbidden there). Godot code only reads simulation state and sends commands.
2. **Commands only.** The player and the AI change the world exclusively through command objects (e.g. `MoveUnits`, `Build`, `SetTax`) queued for the next tick. AI uses the same commands as the player and sees only what a player could see.
3. **Fixed tick.** The simulation advances in fixed ticks. Rendering interpolates between ticks. No game logic in `_Process`.
4. **Determinism.** Same seed + same command log = identical state, on every machine. Therefore in `FutureCity.Sim`:
   - Use only the simulation-owned seeded RNG. Never `System.Random`, `Guid.NewGuid()`, or Godot random.
   - Never read wall-clock time (`DateTime.Now`, `Stopwatch`) for game logic.
   - Use integer or fixed-point math for anything that affects state. No `float`/`double` in state.
   - Iterate collections in a stable order. Never iterate a `HashSet` or `Dictionary` where order affects results.
   - No multithreading that can change results; parallel systems must produce order-independent output.
5. **ECS (Friflo.Engine.ECS).** Systems implement `ISimSystem`, hold no state of their own, and run in the order listed in `SimulationConfig`.
   - Create entities only with `World.CreateEntity()`; it hands out sequential ids that are never reused and survive save/load. Never call `Store.CreateEntity()`.
   - ECS storage order changes after loading a save. If a system's result depends on iteration order (it uses the RNG, creates/deletes entities, or resolves conflicts between entities), iterate with `World.InIdOrder(query)`.
   - Components are structs implementing `IComponent` with public integer/enum/bool fields and a stable `[ComponentKey("...")]`.
6. **Data-driven content.** Gameplay numbers and definitions live in `FutureCity.Content/Data/*.json` (embedded in the assembly), not in code. Definition types and validation live in `FutureCity.Sim/Content`. Every tech, institution and era has a precondition expression evaluated by the emergence engine.
7. **Save/load.** `SaveGame` writes the full state as canonical JSON; `SaveGame.StateHash` is the SHA-256 of that state. Any new world field or component must be saved and covered by the round-trip test. New commands must be registered in `CommandRegistry.CreateDefault()` under a name that never changes.

## Coding conventions

- C# with nullable reference types enabled; treat warnings as errors in `FutureCity.Sim`.
- Small, focused systems; one responsibility per system class.
- Godot node scripts are `partial` classes; keep them thin (presentation and input only).
- Public simulation APIs get XML doc comments.
- No new NuGet or Godot addon dependencies without explicit approval.

## Testing

- Every simulation feature needs xUnit tests in `tests/FutureCity.Sim.Tests`.
- The determinism tests (same seed + commands → same state hash; save, load and continue → same hash as an uninterrupted run) and the save/load round-trip test must always pass.
- `ArchitectureTests` scan `FutureCity.Sim` for forbidden APIs (Godot, `System.Random`, wall-clock time, `float`/`double`, threads). Don't weaken them; fix the code.
- Economy and balance changes: verify with headless runs and report the before/after numbers.

## Godot files

- Scenes (`.tscn`) and resources (`.tres`) are text and may be edited, but keep edits minimal and valid.
- Never edit `.godot/` (generated cache) or `*.uid` files by hand.
- Prefer building UI and procedural content from code where practical.

## Domain accuracy

When adding a technology, institution, unit or crisis, its preconditions and effects must reflect real historical principles. Add a short codex entry (plain text, readable by ages 14+) explaining why it emerged. Keep contested topics neutral and present systems as trade-offs.
