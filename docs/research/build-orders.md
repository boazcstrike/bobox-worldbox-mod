# Research: how WorldBox cities choose what to build

Read-only research against `decompiled/`, 2026-11-08. Paths are relative to `decompiled/`.

## Build order data

- `BuildOrder` (`BuildOrder.cs`) is a public asset. Its fields:
  - `required_pop`, `required_buildings`
  - `limit_type`: the maximum count for that building's type; 0 means no limit
  - `check_full_village`, `check_house_limit`, `min_zones`, `upgrade`
  - `requirements_orders[]`, `requirements_types[]`
- `CityBuildOrderAsset.list` is a public `List<BuildOrder>`. The library is
  `AssetManager.city_build_orders`, with the templates `build_order_basic`,
  `build_order_basic_2`, and `build_order_advanced`.
- `ActorAsset.build_order_template_id` chooses the template per species. Humans, elves, orcs, and
  dwarves use `build_order_advanced`. `City.getActorAsset()` follows the leader's species.
- An order id maps to a building id through the species' `ArchitectureAsset`. A new order id needs
  `arch.addBuildingOrderKey(key, buildingId)`, or the lookup throws.

## How the next building is picked

`CityBehBuild` (`ai.behaviours/CityBehBuild.cs`) is the only step in `tasks_city["build"]`. Every
5 seconds it runs these steps:

1. It skips the tick while the city is in danger or being captured.
2. It stops while a building is under construction. Only one building is under construction at a
   time.
3. It keeps the template entries that pass `canUseBuildAsset` and `hasResourcesForBuildAsset`.
   `canUseBuildAsset` checks the zone minimum, the type limit (buildings under construction
   count), the house limit, population, building count, and requirements.
4. It picks one of them **uniformly at random**. Nothing is prioritised.
5. It calls `tryToBuild`, or `upgradeBuilding` for an upgrade.

These are public statics: `buildTick`, `canUseBuildAsset`, `hasResourcesForBuildAsset`,
`tryToBuild`, `tryToBuildInZones`, `upgradeBuilding`. `City.zones`, `timer_build`,
`under_construction_building`, `getBuildingListOfID`, and `hasEnoughResourcesFor` are internal,
so they need `Game/CityInternals` reflection. `City.countBuildingsType` and `getHouseLimit` are
public.

The game sets no minimum count for any building type. Only a maximum exists, through
`limit_type`.

## Placement

- `tryToBuild` filters the city's zones with the building's flags:
  - `build_place_single`: no other civ buildings in or around the zone
  - `build_place_batch`: the zone is not near single-placed buildings
  - `build_place_borders`: the zone is at the city border
  - `build_place_center`: try the zone centre first
- Within the allowed zones it takes the valid tile closest to the city centre.
- A culture trait made with `CultureTrait.setTownLayoutPlan(checker)` can veto zones. The trait
  only applies once a city has 10 or more houses or 20 or more zones.
- Farms follow the windmill. `CityBehCheckFarms` collects city tiles near the windmill into
  `calculated_place_for_farms`. A windmill needs more than 30 farmable tiles nearby.

## Building types

`type_bonfire`, `type_well`, `type_training_dummies`, `type_stockpile`, `type_statue`,
`type_mine`, `type_windmill`, `type_docks`, `type_watch_tower`, `type_barracks`, `type_temple`,
`type_library`, `type_market`, `type_house`, `type_hall`.

Per-race buildings follow the pattern `house_{arch}_0..5`, `hall_{arch}_0..2`,
`windmill_{arch}_0..1`, `fishing_docks_{arch}`, `docks_{arch}`, and so on. Shared buildings
include `bonfire`, `stockpile`, `mine`, and `well`.

## Recommended hook (no Harmony)

Replace `CityBehBuild` in `tasks_city["build"]` with `BoBoxCityBuild`, using the same pattern as
`BrainInstaller`. Each tick, with a 5-second timer kept per city:

1. Exit when the city is in danger, being captured, or already has a building to build.
2. Take the city's template entries and keep those that pass the game's own checks.
3. Drop types that are at their maximum.
4. Prefer types that are below their minimum.
5. Otherwise pick by weight.
6. Build through the public `CityBehBuild.tryToBuild` or `upgradeBuilding`.

If the step does not bind, keep the stock step in place.

The god-power `AutoCivilization` calls `buildTick` directly, so the swap does not cover it.
