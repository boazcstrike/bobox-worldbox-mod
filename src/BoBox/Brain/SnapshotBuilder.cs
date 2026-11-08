using System;
using System.Collections.Generic;
using BoBox.Game;
using BoBox.Planner;

namespace BoBox.Brain
{
    /// <summary>
    /// Reads a <see cref="CitySnapshot"/> from a city. Call right after the game's own job check,
    /// which fills <c>city.tasks</c> (farm fields, fires, fallback counts) and <c>city.jobs</c>
    /// (default slots).
    /// </summary>
    /// <remarks>
    /// Resource counts come from the city's zones (<c>TileZone.countBuildingsType</c>), not from
    /// <c>city.tasks</c>: the game's check counts hives from <c>BuildingList.Food</c>, so its hive
    /// count equals its bush count (docs/research/terrain-signals.md). Here hives come from
    /// <c>BuildingList.Hives</c>. Like the game, bushes and hives count only buildings with
    /// <c>hasResourcesToCollect()</c> (ripe), trees, bushes, plants and hives count only while their
    /// stockpile has space, and ores always count.
    /// </remarks>
    internal static class SnapshotBuilder
    {
        private const string WindmillType = "type_windmill";
        private const string MineType = "type_mine";

        private static bool _isFallbackLogged;

        public static CitySnapshot Build(City city)
        {
            CityTasksData tasks = city.tasks;
            CityStatus status = city.status;
            IReadOnlyList<TileZone> zones = ReadZones(city);
            bool hasZones = zones != null;
            return new CitySnapshot(
                adults: status.population_adults,
                children: status.population_children,
                food: city.getTotalFood(),
                wood: city.getResourcesAmount("wood"),
                stone: city.getResourcesAmount("stone"),
                gold: city.getResourcesAmount("gold"),
                housingFree: status.housing_free,
                hasStorage: CityInternals.HasStorageBuilding(city),
                hasWindmill: CityInternals.HasFinishedBuildingType(city, WindmillType),
                hasMine: CityInternals.HasFinishedBuildingType(city, MineType),
                pendingBuildings: CityInternals.HasBuildingToBuild(city) ? 1 : 0,
                bushes: hasZones ? CountRipeIfSpace(city, ResourceLibrary.berries, zones, BuildingList.Food) : tasks.bushes,
                plants: hasZones ? CountIfSpace(city, ResourceLibrary.herbs, zones, BuildingList.Flora) : tasks.plants,
                hives: hasZones ? CountRipeIfSpace(city, ResourceLibrary.honey, zones, BuildingList.Hives) : 0,
                trees: hasZones ? CountIfSpace(city, ResourceLibrary.wood, zones, BuildingList.Trees) : tasks.trees,
                ores: hasZones ? Count(zones, BuildingList.Minerals) : tasks.minerals,
                cropTiles: CropTiles(city, tasks),
                fires: tasks.fire,
                baseSlots: BaseSlots(city.jobs));
        }

        // Null when a game update renamed City.zones. The counts then fall back to the game's own
        // city.tasks, which the check just filled with the same space filter. Hives stay 0 there:
        // tasks.hives is the game's bug (it repeats the ripe bush count), so no honey gatherers.
        private static IReadOnlyList<TileZone> ReadZones(City city)
        {
            IReadOnlyList<TileZone> zones = CityInternals.IsSignalsAvailable ? CityInternals.GetZones(city) : null;
            if (zones == null && !_isFallbackLogged)
            {
                _isFallbackLogged = true;
                Log.Warn("city zones unavailable: resource counts fall back to city.tasks, hives counted as 0");
            }

            return zones;
        }

        private static int CountIfSpace(City city, ResourceAsset resource, IReadOnlyList<TileZone> zones, BuildingList list)
        {
            return city.hasSpaceForResourceInStockpile(resource) ? Count(zones, list) : 0;
        }

        private static int CountRipeIfSpace(City city, ResourceAsset resource, IReadOnlyList<TileZone> zones, BuildingList list)
        {
            return city.hasSpaceForResourceInStockpile(resource) ? CountRipe(zones, list) : 0;
        }

        private static int Count(IReadOnlyList<TileZone> zones, BuildingList list)
        {
            int count = 0;
            for (int i = 0; i < zones.Count; i++)
            {
                count += zones[i].countBuildingsType(list);
            }

            return count;
        }

        // Same walk as the game's countResources: only buildings with something to collect now.
        private static int CountRipe(IReadOnlyList<TileZone> zones, BuildingList list)
        {
            int count = 0;
            for (int i = 0; i < zones.Count; i++)
            {
                HashSet<Building> buildings = zones[i].getHashset(list);
                if (buildings == null)
                {
                    continue;
                }

                foreach (Building building in buildings)
                {
                    if (building.hasResourcesToCollect())
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        // Same farmer cap the game uses: planned farm tiles, or existing fields while crops grow.
        private static int CropTiles(City city, CityTasksData tasks)
        {
            int fields = World.world_era != null && World.world_era.flag_crops_grow ? tasks.farms_total : 0;
            return Math.Max(city.calculated_place_for_farms.Count, fields);
        }

        private static IReadOnlyDictionary<string, int> BaseSlots(CitizenJobs jobs)
        {
            var slots = new Dictionary<string, int>();
            foreach (KeyValuePair<CitizenJobAsset, int> pair in jobs.jobs)
            {
                slots[pair.Key.id] = pair.Value;
            }

            return slots;
        }
    }
}
