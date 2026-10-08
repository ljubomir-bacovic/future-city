# Future City — Design Brainstorm

Oct 8, 2026 · @Ljubomir Bacovic

Future City is a single-player-first strategy and city simulation that takes a civilization from hunter-gatherers to a modern economy and on into projected futures. It is educational for teens and adults: accurate about the principles of how societies, economies, science and armies developed, not about exact dates and places.

## Eras

1. **Primitive** – hunting, gathering, survival.
2. **Dark Ages** – farming, villages, barter, first armies.
3. **Medieval** – coinage, markets, guilds, taxation, wars.
4. **Renaissance** – banking, credit, merchant companies, early science.
5. **Early Modern** – joint-stock companies, the stock exchange, professional armies.
6. **Industrial** – factories, wage labour, central banks, mass education, public health.
7. **Modern** – corporations, financial markets, services, technology.
8. **Near and far future** – projections from real trends (energy, automation, demographics, space), shown as branching scenarios.

## Key decisions

| Area | Decision |
| --- | --- |
| Time | Pausable real time; time per tick shrinks in later eras |
| Opponents | Rival AI civilizations in v1; multiplayer in v2 |
| Accuracy | Principles and cause-and-effect, not dates and places |
| Audience | Ages 14–18 and adults; teacher mode for schools |
| Art | Simple, illustrated 2D |
| Stack | Godot 4 (C#) for presentation; deterministic simulation as a standalone .NET library |

## Core principle

The game simulates **why** things happened, not **when**. History becomes a set of cause-and-effect rules; dates are an output of the simulation, not an input.

Nothing unlocks on a timer or by a tech-tree click alone. Each institution or technology emerges when its real-world preconditions exist in the player's society, and the codex explains which preconditions mattered.

## Opening: from survival to war

The game starts with a handful of villagers in a primitive setting and grows into organized states that wage wars. Each stage follows from the surplus the previous one created:

1. **Hunting and gathering.** Villagers forage and hunt to survive; food is the only resource that matters.
2. **Woodcutting and settlement.** Wood enables shelters and tools, and the camp becomes a village.
3. **Farming.** A steady food surplus frees people from food production for other work.
4. **Armies.** Surplus and population make it possible to arm and feed soldiers.
5. **Wars and further development.** Conflict over land and resources pushes organization, technology and the economy forward.

## Emergence from preconditions

| Emerges | Real preconditions |
| --- | --- |
| Markets and money | Food surplus, specialization, trade volume high enough that barter becomes impractical, someone to guarantee coin value |
| Banking | Money in circulation, trust and contract law, merchants needing credit for long trade trips |
| Stock market | Ventures too big or risky for one person, joint-stock law, accounting, a literate merchant class, surplus savings looking for returns |
| Universities and science | Surplus to support non-producers, writing, patrons (church, state, wealthy), later cheap communication (printing) |
| Mass education | Industrial jobs needing literate workers, plus a state able to fund schools |
| Professional armies | Tax income or credit to pay soldiers, which needs a working economy and state capacity |
| Public health | Dense cities causing epidemics, then sanitation, then germ theory, then hospitals and vaccines; life expectancy feeds back into the workforce |

These domains feed each other: health improves the labour supply, education drives science, science raises productivity, productivity creates capital, and capital funds armies and institutions. That feedback is the central lesson of the game.

## Civilizations

Several playable civilizations, each starting with different geography, resources and culture modifiers. The same rules applied to different starting conditions produce different development paths: a coastal trading city develops banking early, an inland agrarian state builds armies first. This also teaches why history unfolded differently in different places.

## Audience: 14–18 and adults

- Real charts, price graphs, order books and interest rates are fine, with progressive disclosure: simple by default, detailed on demand.
- The codex has two layers: a short explanation, then an optional "go deeper" section with economics and history references.

## Teacher mode

- Scenarios that isolate one principle, for example "cause a bubble and watch it burst", "fund a plague response" or "build a bank".
- A classroom setup with shared scenarios and a post-game debrief comparing the class's results with the real principle and historical cases.
- Potential funding sources: EU education grants and Erasmus+ projects.

## Art style

Simple, illustrated and readable, in a hand-drawn map and board-game aesthetic. It is cheap to produce, ages well, suits 2D in Godot, and fits an educational tone.
