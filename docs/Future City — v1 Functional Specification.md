# Future City — v1 Functional Specification

Oct 8, 2026 · @Ljubomir Bacovic

v1 is a short single-player game (15–20 minutes) against rival AI that takes a civilization from hunter-gatherers to a medieval market town (Primitive, Dark Ages and Medieval eras), built on principle-driven emergence. It ships as **Future City - Origins**, free, in English on Steam for Windows.

## Scope

v1 proves the core idea end to end: emergence from preconditions, a living economy, and conflict, across three eras, kept small so it can ship soon.

| Area | In v1 | Later versions |
| --- | --- | --- |
| Eras | Primitive, Dark Ages, Medieval | Renaissance, Early Modern, Industrial, Modern, Near and far future |
| Session length | 15–20 minutes per game | Longer games and campaigns |
| Players | 1 human vs 1–3 rival AI | Multiplayer (v2) |
| Civilizations | 3 (below) | More civilizations |
| Population | Up to \~100 citizens per civilization (tunable) | Larger populations |
| Map | Small generated maps | Larger maps, scenario maps |
| Economy | Shared stores, barter, money, markets, taxes, guilds | Banking, joint-stock companies, stock exchange, central banks |
| Modes | Sandbox vs AI, 2–3 tutorial scenarios | Teacher mode, campaigns |
| Platform | Windows, on Steam | macOS, Linux, others |
| Price | Free | Premium offering |
| Language | English | Localizations |

### Civilizations in v1

| Civilization | Starting conditions | Natural path |
| --- | --- | --- |
| Venetians | Coastal lagoon, fish, timber, little farmland | Trade, ships, early money and markets |
| Franks | Fertile river plains and forests | Farming surplus, larger population, feudal levies and heavy cavalry |
| Mongols | Open steppe, horses, few forests | Herding, mobility and cavalry raids; weaker farming and towns |

## Game setup and session flow

- **New game:** choose civilization, map size, number of rival AI (1–3), difficulty, and random seed.
- **Start state:** a few villagers in the wild with no buildings and no technology.
- **Time:** pausable real time with speeds 1× to 4×. Orders can be given while paused. A typical game reaches the Medieval era in 15–20 minutes, so in-game years per minute are high in early eras and shrink later.
- **Save and load:** at any time; autosave at intervals.
- **Victory conditions** (chosen at setup): first to found a chartered market town (mint, marketplace, guild and written law in place); highest prosperity score at a 20-minute limit; or conquest. Sandbox mode has no end.
- **Defeat:** the civilization loses all its settlements or its population reaches zero.

## Population and villagers

- Every citizen is a simulated agent with age, health, needs (food, shelter, safety, later goods), a job, and, once money exists, savings.
- Population grows with food surplus and housing, and falls with famine, disease and war. v1 caps it at about 100 citizens per civilization (tunable).
- **Control shifts over time.** Early on, the player gives villagers direct orders (gather, hunt, build). Later, citizens choose jobs themselves based on demand and wages, and the player steers through buildings, taxes and laws.
- **Social classes emerge** from the economy: peasants, craftsmen, merchants, clergy and nobility.
- Happiness depends on needs met, taxes, safety and health. Low happiness reduces productivity and can lead to unrest.

## Resources and production

- **Natural resources** on the map: game animals, wild plants, forests, fertile land, stone, clay, ore, fish, water. They are finite or renewable at realistic rates, so overhunting and deforestation have consequences.
- **Production chains** start shallow and deepen with each era, for example grain → flour → bread, ore → iron → tools and weapons, wool → cloth → clothing, timber → ships.
- **Buildings** host production and storage. Productivity depends on workers, tools, technology and inputs.
- **Logistics:** goods are physically carried by people, carts and later ships. Distance and roads affect cost and speed.
- **Specialization:** settlements become good at what their location supports, which creates the need to trade.

## Economy

The economy evolves in stages, each unlocked by its real preconditions. Prices always come from supply and demand, never from fixed tables.

1. **Shared stores** — the tribe pools food and materials.
2. **Barter** — households and settlements exchange goods directly.
3. **Money** — coinage minted by an authority; the player controls coin quality, and debasing it causes inflation.
4. **Markets, taxes and guilds** — marketplaces set prices; guilds regulate crafts; the player sets taxes and tariffs; simple money-lending appears at the end of the era.

Banking, joint-stock companies and the stock exchange come in later versions.

The player sees prices, trade flows and the treasury balance, with a simple view by default and price history on demand.

## Research, emergence and institutions

- **Emergence rule:** every technology and institution has preconditions (resources, population, surplus, other technologies, institutions). It becomes available only when they are met; the UI shows what is missing.
- **Research** needs people freed from production and a place to work: elders and shamans, then monasteries.
- **Discoveries** are partly deliberate (funded research) and partly by chance, weighted by how much activity happens in that field.
- **Institutions in v1:** chiefdom and kingship, written law and property rights, temples and monasteries, guilds, the mint, markets, a town charter, early public health (wells, quarantine).
- **Era transitions** happen when enough key institutions and technologies are in place, not by paying a fixed cost.

## Military and conflict

- **Military evolves with the economy:** armed hunters, then levies of peasants, then paid mercenaries once money exists.
- **Armies cost food and labour.** Soldiers are taken from the workforce and must be fed and equipped; paid troops need a treasury.
- **Units in v1:** clubmen and hunters, spearmen, archers, cavalry (horse archers for the Mongols, heavy cavalry for the Franks), rams, and transport and war boats.
- **Combat** is real-time, AoE2 style, with direct unit control and simple fortifications (palisades, then stone walls).
- **War has economic effects:** disrupted trade, destroyed buildings, deaths and loot.
- **Diplomacy:** peace, alliance, trade agreement, tribute, war.

## Rival AI

- AI civilizations play by the same simulation rules and through the same commands as the player. Difficulty adds bonuses but never breaks the rules.
- **Personalities** such as trader, conqueror and builder shape priorities and diplomacy.
- AI civilizations trade with the player, compete for land and resources, form alliances and declare war.
- They take part in the shared economy: their merchants use the same markets.

## Events and crises

Crises emerge from the simulation rather than from scripts. External shocks such as weather and disease outbreaks are random, but how severe they become depends on the state of the society.

- **Famine:** crop failures or overhunting combined with low food reserves.
- **Epidemics:** more likely and deadlier in dense settlements without clean water.
- **Inflation:** caused by coin debasement.
- **Raids:** neighbours attack when defences are weak and stores are full.
- **Unrest:** caused by high taxes, hunger or inequality.

After each major crisis the game shows a short explainer of what happened, why, and the real historical parallel.

## Codex and educational features

- **Codex:** an illustrated entry for every resource, technology, institution, unit and crisis. Each entry has a short explanation and an optional "go deeper" layer with history and economics.
- **Learning by consequence:** a concept is explained when the player first experiences it (first inflation, first loan default), not up front.
- **"Your history vs real history":** after key moments, a short comparison of what happened in the game and what happened historically.
- **Tutorial scenarios:** 2–3 short scenarios that each teach one principle: surviving a famine, why debasing coins causes inflation, and defending against a raid.
- **Tone:** neutral and fact-based on contested topics; systems are presented as trade-offs.

## UI and controls

- **Main view:** an illustrated 2D isometric map with RTS camera, box selection and right-click orders.
- **Top bar:** key resources, treasury, population, current era, date, and game speed with pause.
- **Dashboards** that unlock with their systems: population, production, markets and prices, treasury, research, military, diplomacy.
- **Overlays:** fertility, resources, happiness, health, trade routes.
- **Notifications** for events, discoveries and crises, each linking to the relevant codex entry.
- **Progressive disclosure:** the interface starts minimal and grows as the civilization grows.

## Decisions and open questions

- [x] v1 scope: Primitive to Medieval, kept small to ship sooner.
- [x] Civilizations: Venetians, Franks, Mongols.
- [x] Session length: 15–20 minutes per game.
- [x] Population: about 100 citizens per civilization, to be tuned.
- [x] Language: English only.
- [x] Distribution: Steam.
- [x] Business model: v1 is free; a premium offering comes later.
- [x] Title: v1 ships as "Future City - Origins", since it ends in the Medieval era.
- [x] Economic actors: families (huts) own goods and coins; gatherers, farmers and crafters work for their family; the chief's treasury owns public buildings and pays for public work.
- [x] Before rival AI, settlements trade with visiting foreign merchants, who pay tariffs.
- [x] Before coins, the chief is paid tribute in goods; afterwards taxes are paid in coins.
