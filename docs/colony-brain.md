# Colony brain: design v2

## Goal

Passive play. A few settlers start on empty land with no city. With no player input they
found a city, then run it like a self-managing settlement: food first, then shelter and
storage, then wood, stone, and the rest. The brain improves how WorldBox's own AI spreads
settlers across the jobs and buildings that already exist in the game. It reads each city's
land, sorts the city into a scenario, and lets that scenario steer jobs and build order. No new
building types yet.

## What WorldBox already does

- Units without a city found one by themselves (`BehCheckBuildCity`) when the world law
  `world_law_kingdom_expansion` is on.
- Each city runs the task `do_checks` from `AssetManager.tasks_city`. One of its steps is
  `CityBehCheckCitizenTasks`, which recomputes `city.jobs` (open job slots) from fixed weights.
  The same step also runs in `do_initial_load_check`.
- The task `build` runs `CityBehBuild`, which picks the next building from
  `AssetManager.city_build_orders`.
- `City.setCitizenJob` hands idle citizens the open slots.
- Gates: farmers only get slots when the city has a `type_windmill`. Gatherers only get slots
  when the city has a storage building. Miners need `type_mine`.

## Hooks: no Harmony

`BehaviourTaskCity.list` is a public list of behaviour steps. At startup BoBox swaps two steps:

- Every `CityBehCheckCitizenTasks` in `do_checks` and `do_initial_load_check` becomes
  `BoBoxCitizenTasks` (the job brain).
- `CityBehBuild` in `tasks_city["build"]` becomes `BoBoxCityBuild` (the build brain).

Both subclasses fall back to the game's own step when the brain is off or when anything throws.
Errors are logged with the `[BoBox]` prefix. The game never crashes because of the brain.

The job brain:

1. calls `base.execute(city)`, so the game still fills `city.tasks` (fires, resources, roads)
   and the default `city.jobs`;
2. builds a `CitySnapshot` from the city;
3. asks the planner for a `JobPlan`, using the city's pathway and capacity limits;
4. rewrites `city.jobs` with the plan.

## Pure layer

`Planner/` and `Rules/` form one pure layer: no `UnityEngine`, no game types, no file IO. All
of it runs in unit tests outside the game. The planner never calls `RulesLoader`, `RulesJson`, or
Newtonsoft; the game layer (`Brain/`) loads the rules and passes a `ColonyRules` in.

## Scenarios

### Signals

`Brain/SignalsReader.cs` reads each city's land from its zones, never from `city.tasks`:

- trees, minerals, farmable share, rock share and harsh share per zone. A harsh tile (sand,
  snow, or a hostile biome) counts once even when it carries more than one harsh layer;
- coast: whether the city touches the ocean, its ocean share, and the island size and owned share;
- the ring of zones around the city: foreign share, unclaimed share, free zones in range;
- the nearest foreign city and the count of hostile neighbour kingdoms.

The coast and island check is the expensive one, so it is cached for `coast_cache_seconds`
(30 s by default).

### Refresh cadence

`Brain/ScenarioCache.cs` refreshes cities round-robin on the main thread, on world time
(`getCurWorldTime`). World time follows game speed and stops while paused.

- A city is due when it was never refreshed, when its last refresh is `refresh_seconds` old
  (8 s by default), or at once when its zone count changes.
- Budget per tick (`Planner/Scenario/RefreshBudget.cs`):
  `max(cities_per_frame, ceil(cities × elapsed / refresh_seconds))`, capped at
  `SignalBounds.MaxCitiesPerFrame`. With many cities the budget grows so each one still
  refreshes about once per interval. Elapsed time counts at most 0.5 s, so a frame hitch does
  not trigger a burst of refreshes. With hundreds of cities at high game speed the cap of
  `MaxCitiesPerFrame` binds first, so a full cycle can take longer than `refresh_seconds`
  (data goes stale for longer; nothing breaks).
- The cache does nothing while the game is loading a world. On the first tick after the load
  every city is re-read.
- `ScenarioCache.MarkAllDue()` makes every city due on the next tick. It is safe from any thread
  (an `Interlocked` flag in `Planner/Scenario/AllDueFlag.cs`). It runs after the startup rules
  load and after every good `POST /rules/reload`. The tick takes the flag before it reads the
  rules, so a reload never refreshes a city with the old rules.
- A new or loaded world clears the cache and restarts the tick clock.
- The cache only ticks when `CityInternals.IsSignalsAvailable`. If the reflection lookup failed,
  this is logged once at install.

### Tags

`Planner/Scenario/ScenarioClassifier.cs` scores 7 tags. A tag with a score above 0 is on. Each
condition scores 0.5 on its threshold and rises to 1.0 one full threshold past it. All
thresholds live in `rules.scenarios`.

A `min_*` threshold of 0 does not switch a tag off: every value passes it, so the tag turns on
for every city. The loader warns about it. To disable a pathway, remove it from the list or
give it a tag that cannot match.

| Tag | On when |
|---|---|
| `forest_rich` | trees per zone ≥ `min_trees_per_zone` |
| `fertile_plains` | farmable share ≥ `min_farmable_share` and rock share < `max_rock_share` |
| `rocky` | rock share ≥ `min_rock_share` or minerals per zone ≥ `min_minerals_per_zone` |
| `scarce` | trees, minerals and farmable share all at or under their maximums, or harsh share ≥ `min_harsh_share` |
| `coastal` | touches the ocean with ocean share ≥ `min_ocean_share`, or a small island, or most of its island owned. Coastal and island are one tag. |
| `crowded_frontier` | foreign ring share, nearest foreign city distance, or hostile neighbours past their thresholds |
| `open_frontier` | unclaimed ring share and free zones in range both past their thresholds |

### Pathways

`rules.pathways` is an ordered list. The first pathway whose `when` matches the city's tags wins.
`when.required` means all of those tags, `when.any` means at least one. The default file order is:

1. `scarce`
2. `crowded`
3. `coastal`
4. `rocky`
5. `forest`
6. `fertile`
7. `open_frontier`
8. `default` (always matches)

A pathway carries three things: `job_weights`, `building_priority`, and
`capacity_multipliers` for jobs and buildings.

**Hysteresis** (`Planner/Scenario/PathwayHold.cs`): a city keeps its current pathway until a
different pathway wins 2 refreshes in a row **and** at least `refresh_seconds` of world time have
passed since that challenger's first win. A win for the held pathway, or for a third pathway,
resets the challenger. The time gate matters in border wars: zone changes trigger extra
refreshes, and without it two quick wins would flip the pathway. A switch takes about one
refresh interval after the first win (about 8 s at defaults, also after a reload). A held
pathway that the reloaded rules no longer list gives way at once.

## Capacity: minimum and maximum per job and building

`Planner/Capacity/CapacityModel.cs` turns a city's area and population into a minimum and a
maximum for every job and building type listed in `rules.capacity`. Unlisted ids are unbounded.

- **Land** is land tiles / 64 (one zone is 8 × 8 tiles), so a half-water zone counts as half.
- **Scale** comes from `rules.map_scale` by map size: 1.0, then 1.25 from size 9, 1.5 from
  size 12, and 1.75 from size 15.
- **Multiplier** is the pathway's `capacity_multipliers` entry (negative counts as 0; NaN or
  infinity count as 1).
- **Max** = min(ceiling, area max).
  - Ceiling = max(⌊`max_ceiling` × scale × multiplier⌋, ⌊`max_per_pop` × population × multiplier⌋).
    So a populous city outgrows the fixed ceiling.
  - Area max = ⌈`max_per_zone` × land × scale × multiplier⌉. A `max_per_zone` of 0 or less
    means only the ceiling applies.
- **Min** = max(`min_floor`, ⌊`min_per_pop` × population × multiplier⌋). The map scale does not
  touch the minimum. A job minimum never exceeds the adults.
- When min > max, the max wins. Land is a hard limit; the minimum is only a wish.

Tuned defaults (`Rules/DefaultRules.cs` and `rules/bobox-rules.json`, kept in sync by a test):

- `max_per_pop`: farmer 0.4, gatherer_bushes 0.1, gatherer_herbs 0.05, gatherer_honey 0.02,
  woodcutter 0.3, miner_deposit 0.05. Everything else 0 (fixed ceiling only).
- `type_house` `min_per_pop` is 0.1 (was 0.2). The old value assumed 5 people per house, but
  upgraded houses hold more (human houses 3 to 7, dwarf houses up to 10, per `BuildingLibrary`),
  so cities built nothing but houses for long stretches.
- Result: a 200-zone city with 300 adults on the neutral pathway employs 288 adults (96 %).
  Before tuning it employed 152. A planner test pins this.

## Job planner

`ColonyPlanner.Plan(CitySnapshot, Pathway, Limits) -> JobPlan` is a pure function.

`CitySnapshot`: adults, children, food total, wood, stone, gold, housing free slots, has
storage, has windmill, has mine, buildings waiting to be built, nearby bushes, plants, hives,
trees, ores, crop tiles, fires.

`JobPlan`: slot count per job id (`builder`, `farmer`, `gatherer_*`, `woodcutter`, `miner`,
`hunter`, `road_builder`, `cleaner`), a short reason for each line, and `Idle`: adults left over
once every open job is at its cap. The summary ends with `idle=N`, and `GET /colony` shows
`idle{adults,reason}`.

Order of needs, highest first:

1. **Warriors and fires:** kept from the game.
2. **Food:** target is `max(30, 5 × adults)`. Under target, food gets 70 % of the remaining
   adults; at target, 25 %.
3. **Builders:** while a building waits, 1 per 3 adults up to 6, times the builder weight,
   lowered to the capacity maximum. **One builder is reserved right after the warriors, before
   food**, so it stays at least 1 even in a starving city, at weight 0, or with a builder maximum
   of 0: construction must always be able to finish. This is the one place a job may exceed its
   capacity maximum. Only when the game's warriors take every adult is there no builder.
   Known trade: a starving city with a single adult puts that adult on the building, so food
   stays at 0 until the building is done. This is bounded, because only the one building under
   construction counts as pending.
4. **Wood:** buffer 40. Under it, wood gets 60 % of the remaining adults; above it, 20 %.
5. **Stone and ore:** buffer 30. Under it, 40 %; above it, 15 %. Mining waits while the city is
   starving or low on wood, but only while a food or wood job still has an open slot (judged
   from the job caps after the food and wood phases, not from raw sources).
6. **Roads and cleaners:** kept from the game, only lowered to their maximum.
7. **Capacity minimums:** food, wood and (when mining is allowed) stone jobs below their minimum
   are raised to it while adults remain, never above the job's cap.
8. **Surplus:** adults left over go to food first while starving; otherwise to wood if low,
   then food, then wood, then stone. Once food and wood have no room left, stone opens even if
   mining was waiting, so adults never idle while ores have open slots.

How the pathway steers it:

- Across groups (food, wood, stone), the fixed share is the base. It is multiplied by the mean
  pathway weight of the group's open jobs, capped at all remaining adults.
- Inside a group, slots split in proportion to each job's weight (Sainte-Laguë), counting slots
  the job already holds. A weight of 0 or less closes the job; an unlisted job weighs 1.
- Jobs kept from the game (warriors, roads, cleaners) ignore pathway weights on purpose: a
  pathway cannot switch off defence or upkeep. They are only lowered to their capacity maximum.
- No job goes above its capacity maximum (except the builder floor above). Slots cut by a
  maximum flow on to the next needs.
- Total slots never exceed adults.

## Build brain

`Brain/BoBoxCityBuild.cs` replaces the game's `CityBehBuild` step and keeps its gating: its own
5 s timer per city, candidates from the game's `canUseBuildAsset` and
`hasResourcesForBuildAsset`, and the build through `tryToBuild` or `upgradeBuilding`. An upgrade
candidate also needs the city to afford the `upgrade_to` asset.

`Planner/Build/BuildPlanner.cs` picks one candidate:

1. Drop new-building types already at their maximum.
2. If any type is below its minimum, pick among those by pathway priority.
3. Otherwise pick at random by weight: weight = 1 + max(0, priority). An unlisted type weighs 1,
   so it stays buildable.

Upgrades replace a building instead of adding one. They never count toward a type's maximum and
never satisfy its minimum.

Retries and failure memory (`Planner/Build/BuildFailureMemory.cs`):

- One tick tries up to 3 picks and up to 5 upgrade targets. This differs from stock on purpose.
- A failed build (no free tile, vanished upgrade target) is remembered for 30 s of world time and
  left out of the candidates, so one blocked pick cannot starve the rest.
- New builds are keyed by building type. Upgrades are keyed by `"U-" + OrderId`, so two upgrade
  orders of the same type fail independently.
- The random seed is the world time in milliseconds, so fast ticks do not repeat a seed.
- If the brain throws after it placed a building or started an upgrade, the stock step does not
  also run that tick. Any other throw runs the stock step, so a repeating brain error never stops
  a city from building. Roads do not count as an action: the game's road call is idempotent.
  Errors after an action are counted per city and logged every 100th time.

Robustness (`Brain/BuildCandidateFinder.cs`, `Brain/BuildWorldWatch.cs`,
`Planner/Build/BuildErrorDedupe.cs`):

- Each build order is evaluated in its own try/catch. A broken order (an architecture without
  that order id, a missing asset) is skipped and logged once; the other orders stay offered.
- Orders on cooldown are dropped before the game's `canUseBuildAsset`, so a blocked dock order
  does not repeat the ocean scan every tick.
- An upgrade target that is not usable (burning, being removed, a ruin) is skipped.
- A stale `under_construction_building` is cleared through the game's `hasBuildingToBuild`
  before the brain adopts an unfinished building, and only usable buildings are adopted.
- On a new or loaded world, every per-city map (timers, failure memory, error dedupe) is cleared,
  so a reused city id does not inherit another city's state.
- Each error message is logged at most once per 300 s of world time per city (up to 8 messages
  tracked per city), so an error that comes and goes does not flood `Player.log`.

## Rules file and reload

The live rules file is `StreamingAssets/mods/bobox-rules.json`. The repo copy is
`rules/bobox-rules.json`.

- `scripts/deploy.sh` copies the rules file only when none is installed, or with
  `--force-rules`. When it keeps an installed file that differs from the repo copy, it prints a
  notice.
- The mod loads the file at startup. `POST /rules/reload` reloads it at runtime.
- A broken file keeps the last good rules and returns the errors. A file that disappears
  between edits (an editor's delete and re-create) also keeps the last good rules: the
  defaults are used only when no file was ever loaded. Out-of-range values are clamped
  (`Rules/RulesSanitizer.cs`, ranges in `Rules/SignalBounds.cs`), including numbers written as
  strings (`"1e9"`, `"NaN"`). Missing fields fall back to the defaults.
- A tag that is not one of the 7 names (a number, a typo) fails the load, so the last good rules
  stay. An unknown job or building id in `capacity` or in a pathway's maps gives a warning.
- At most 50 warnings are returned and logged, plus a "N more warnings suppressed" line.
- Loading never throws: any unexpected error becomes a failed load with a message.
- After a good load, `MarkAllDue` refreshes every city with the new rules. Each city in
  `GET /colony/scenarios` reports the `rules_loaded_at` it was classified with, so you can see
  when it picked up the change.

## Map sizes

Sizes 10 to 16 are added to the game's size list. Size 16 is 1024 × 1024 tiles.
`Config.maxMapSize` stays `iceberg`, so Workshop upload stays blocked for large maps.

Known issue: on size 16 the game's `ExplosionChecker` misbehaves at y ≥ 1000. This is documented
only; fixing it would need Harmony.

## Settler start

`POST /scenario/settlers {"asset":"human","x":..,"y":..,"count":5}` spawns adult units on an
empty spot and makes sure `world_law_kingdom_expansion` is on, so they found a city by
themselves.

## Visibility

- `GET /colony` lists cities with their last plan, reasons and idle adults.
- `GET /colony/scenarios` lists each city's signals, tags, pathway, limits, `rules_loaded_at`,
  and its last build choice with reason and `last_build.at`.
- `GET /colony/log` returns the last 200 decisions from a ring buffer.
- `GET /rules` returns the active rules and load state.
- `POST /brain {"enabled":true|false}` turns the brain on or off.

## Decisions

- No Harmony. Step swaps only.
- Coastal and island are one tag.
- World time is the clock for refreshes and build failure memory.
- Pathway weights split slots inside a group and scale the group's share. Fixed shares stay
  the cross-group base.

## Later

- Tune thresholds and ceilings after in-game play: coastal share, house throttle, idle adults.
- New buildings for lumber, gathering, and workshops.
- Job choice by skill and traits (`City.setCitizenJob`). That one needs Harmony.
- A publicized Assembly-CSharp to drop the reflection in `CityInternals`.
