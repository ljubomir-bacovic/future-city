# CLAUDE.md

@AGENTS.md

The project rules above apply to every session. Below is the Claude Code–specific workflow.

## Workflow

1. **Plan first** for anything touching more than one system or project: use plan mode, list the files to change and the tests to add, then implement.
2. **Simulation first.** Build and test features in `FutureCity.Sim` headless before wiring them into Godot. You can only see the game through screenshots, so keep as much logic as possible where tests can verify it.
3. **Verify, don't assume.** After each change run `dotnet build FutureCity.sln` and `dotnet test`. For gameplay or economy changes, also run the headless runner and report key numbers (population, prices, time to era).
4. **Small commits.** One logical change per commit with a clear message. Don't commit failing builds.
5. **Track the plan.** v1 work follows the phases in `docs/`. Say which phase and task a change belongs to.

## Things to check before finishing

- [ ] No `using Godot;` in `FutureCity.Sim`
- [ ] No `float`/`double`, `System.Random` or wall-clock time in simulation state or logic
- [ ] New components are saved/loaded and covered by the round-trip test
- [ ] Gameplay numbers are in `FutureCity.Content` JSON, not hard-coded
- [ ] Build and all tests pass

## Godot specifics

- Ask before editing a scene the user may have open in the Godot editor; unsaved editor changes and file edits clash.
- Check static visuals yourself: run the game with `--screenshot=<scratchpad path> --frames=N` (see AGENTS.md) and read the PNG. Animation feel, input responsiveness and art taste still need the user's eyes: describe what to check.
- Godot C# scripts must be `partial` classes with the class name matching the file name.

## When unsure

Ask rather than guess on game design decisions (balance targets, new mechanics, historical interpretation). Make reasonable engineering decisions yourself and state them.
