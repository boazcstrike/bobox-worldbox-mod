# Research: per-city terrain and neighbour signals

Read-only research against `decompiled/`, 2026-11-08. Paths are relative to `decompiled/`.

## Cheap per-city reads

A city owns `City.zones` (internal). A `TileZone` is 8×8 tiles. Each zone keeps public, O(1)
counts:

- **Buildings by kind.** `zone.countBuildingsType(BuildingList.X)`, where X is Trees, Minerals,
  Food (bushes), Flora (plants), Hives, Wheat, or Ruins.
- **Tiles by type.** `zone.getTilesOfType(type)?.Count`, or `zone.getTileTypes()` for every type
  at once.
  - Each tile counts under both its main type and its top type (grass sits on soil), so sum each
    layer separately.
- **Ground and liquid.** `zone.tiles_with_ground` and `zone.tiles_with_liquid`. Liquid includes
  lava, so it is not a water signal.
- **Map position and neighbours.** `zone.city`, `zone.x`, `zone.y`, `zone.neighbours_all`,
  `zone.chunk`.

Internal sets on `City` (read them through `Game/CityInternals`):

- `border_zones`, `neighbour_zones` (the outside ring);
- `neighbours_cities`, `neighbours_kingdoms`;
- `kingdom`.

The game rebuilds these during its own status update. Read them; never trigger a rebuild.

## Terrain facts

- No "forest" tile exists. Forest means trees per zone, counted from `BuildingList.Trees`. The
  world generator places at most 3 trees per zone.
- No fertility value exists. Use these as a proxy:
  - tile types with `can_be_farm`;
  - top types with `grass`;
  - farm field tiles (`TopTileLibrary.field`).
- Rock: `TileLibrary.hills`, `mountains`, `summit`.
- Water: `deep_ocean`, `close_ocean`, `shallow_waters`. A lake uses the same tile types. Tell
  them apart by the size of the water island: docks need at least 2,500 tiles.
- Biomes: top tile `biome_asset`. Restricted biomes set `only_allowed_to_build_with_tag`. Snow,
  permafrost and ice are top types.
- Island size: `city.getTile().region.island.getTileCount()`. The value is cached. A city needs at
  least 300 island tiles to be founded.

## Do not read `city.tasks` for classification

`CityBehCheckCitizenTasks` fills `city.tasks` unreliably:

- trees, bushes and herbs only count when the matching stockpile has space;
- hives are counted from `BuildingList.Food`, which is a game bug.

Count from zones instead. This also matters to the colony planner. It reads `tasks.hives`, which
equals `tasks.bushes`.

## Never call these from the mod

| Call | Why |
| --- | --- |
| `isGoodForNewCity` | Triggers a world-wide recalculation |
| `CityZoneGrowth.getZoneToClaim` | Mutates a shared worker |
| `CityBehBuild.getDockTile` | Clears static pools |
| `Kingdom.isEnemyAroundZone` | Returns wrong answers |

## Free land

- **Fast version.** Count `neighbour_zones` where `city == null`, ground > 0, and no lava.
- **Wider version.** Scan zones within ±9 zones of the city-tile zone on the same island. The
  game's own claim radius is about 9.75 zones.

## Cadence

Recompute each city's signals every 5 to 10 seconds of game time, round-robin across cities.
Also recompute when the city's zone count changes. Island and coast signals change rarely, so
recompute them every 30 seconds or more.

## Signal set (for a pure classifier)

| Signal | Source |
| --- | --- |
| zones, land tiles | `zones.Count`, sum of `tiles_with_ground` |
| trees, minerals, bushes, plants, hives | zone `countBuildingsType` |
| farmable, grass, field tiles | zone tile types |
| hills, mountain, sand, snow, lava, hostile-biome, ocean tiles | zone tile types |
| dominant biome | most common top-type `biome_asset` |
| coastal | a city zone touches an ocean-layer island with at least 2,500 tiles |
| island tiles, share of island owned | island `getTileCount()` |
| ring zones, unclaimed land in ring, foreign zones in ring | `neighbour_zones` |
| free zones in range | ±9-zone scan |
| neighbour cities, foreign and hostile neighbour kingdoms | `neighbours_*`, `Kingdom.isEnemy` |
| nearest foreign city distance | `World.world.cities`, zone x,y squared distance |
| at war | `kingdom.hasEnemies()` |
| population, max houses | `city.status` |

Starting thresholds. These are guesses to tune through the rules file:

| Scenario | Rule |
| --- | --- |
| Forest-rich | 2 or more trees per zone |
| Fertile plains | farmable land above 60%, and hills plus mountain below 10% |
| Rocky or mountain | hills plus mountain above 25%, or 1 or more minerals per zone |
| Scarce | trees, minerals and farmable land all low, or a lot of hostile, snow or sand terrain |
| Coastal | coastal is true |
| Island | island below 2,500 tiles, or the city owns more than 50% of the island |
| Crowded frontier | foreign zones above 30% of the ring, a foreign city within 6 zones, or a hostile neighbour |
| Open frontier | unclaimed land above 50% of the ring, and 10 or more free zones in range |

## Possible simplification (spike)

Many reads go to internal members. Reflection works but is verbose. Unity Mono mods often
compile against a publicized copy of `Assembly-CSharp` so internals can be used directly. Test
this on WorldBox's Mono before adopting it. It is not part of the current plan.
