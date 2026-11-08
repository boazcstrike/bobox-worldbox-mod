using System;
using System.Collections.Generic;
using BoBox.Game;
using BoBox.Planner.Scenario;
using BoBox.Rules;

namespace BoBox.Brain
{
    /// <summary>
    /// Reads one city's <see cref="ScenarioSignals"/> from its zones (docs/research/terrain-signals.md),
    /// never from city.tasks, and never through the calls that document forbids. Main thread only.
    /// Internal City members come from Game.CityInternals (signals group).
    /// </summary>
    internal static class SignalsReader
    {
        private const int ZoneSizeTiles = Config.CITY_ZONE_SIZE;

        private static TerrainRefs _refs;

        public static ScenarioSignals Read(City city, SignalRules rules, CityCenters centers)
        {
            TerrainRefs refs = Refs();
            IReadOnlyList<TileZone> zones = CityInternals.GetZones(city);
            Kingdom kingdom = CityInternals.GetKingdom(city);
            WorldTile anchor = CityAnchor.Tile(city, zones);
            TileZone center = anchor?.zone ?? (zones.Count > 0 ? zones[0] : null);
            TileIsland island = anchor?.region?.island;

            var totals = new TerrainTotals();
            for (int i = 0; i < zones.Count; i++)
            {
                totals.AddZone(zones[i], refs);
            }

            CoastReading coast = CoastReader.Read(city, zones, island, refs, rules);
            RingCounts ring = CountRing(city, refs);
            int foreignKingdoms = 0;
            int hostileKingdoms = 0;
            CountNeighbourKingdoms(city, kingdom, ref foreignKingdoms, ref hostileKingdoms);

            return new ScenarioSignals(
                zones: zones.Count, landTiles: totals.LandTiles,
                trees: totals.Trees, minerals: totals.Minerals, bushes: totals.Bushes,
                plants: totals.Plants, hives: totals.Hives,
                farmableTiles: Math.Max(totals.FarmableMain, totals.FarmableTop),
                grassTiles: totals.Grass, fieldTiles: totals.Field,
                hillsTiles: totals.Hills, mountainTiles: totals.Mountain, sandTiles: totals.Sand,
                snowTiles: totals.Snow, lavaTiles: totals.Lava, hostileBiomeTiles: totals.HostileBiome,
                harshTiles: totals.HarshTiles,
                oceanTiles: totals.Ocean, dominantBiome: totals.DominantBiome(),
                isCoastal: coast.IsCoastal, islandTiles: coast.IslandTiles,
                islandOwnedShare: IslandShare(totals.LandTiles, coast.IslandTiles),
                ringZones: ring.Zones, ringUnclaimedZones: ring.Unclaimed, ringForeignZones: ring.Foreign,
                freeZonesInRange: CountFreeZones(center, island, refs, rules.FreeZoneScanRadius),
                neighbourCities: CityInternals.GetNeighbourCities(city).Count,
                foreignNeighbourKingdoms: foreignKingdoms, hostileNeighbourKingdoms: hostileKingdoms,
                nearestForeignCityZones: centers.NearestForeign(city, kingdom, center),
                isAtWar: kingdom != null && kingdom.hasEnemies(),
                population: city.status.population, maxHouses: city.status.houses_max);
        }

        /// <summary>Drops the coast cache of every city not in <paramref name="aliveIds"/>.</summary>
        public static void Forget(ISet<long> aliveIds) => CoastReader.Forget(aliveIds);

        /// <summary>Drops every cached coast reading, for a new world.</summary>
        public static void Clear() => CoastReader.Clear();

        // The tile libraries are filled when the game loads assets; build the refs on first use after that.
        private static TerrainRefs Refs()
        {
            if (_refs != null)
            {
                return _refs;
            }

            if (TileLibrary.hills == null || TopTileLibrary.field == null)
            {
                throw new InvalidOperationException("tile libraries are not loaded yet");
            }

            _refs = new TerrainRefs();
            return _refs;
        }

        private static double IslandShare(int landTiles, int islandTiles) =>
            islandTiles <= 0 ? 0 : Math.Min(1.0, landTiles / (double)islandTiles);

        // Free land: unowned, has ground, no lava. Same test for the ring and the wide scan.
        private static bool IsFreeLand(TileZone zone, TerrainRefs refs)
        {
            return zone.city == null && zone.tiles_with_ground > 0 && !HasAnyTile(zone, refs.Lava);
        }

        internal static bool HasAnyTile(TileZone zone, TileTypeBase[] types)
        {
            for (int i = 0; i < types.Length; i++)
            {
                if (zone.hasTilesOfType(types[i]))
                {
                    return true;
                }
            }

            return false;
        }

        // Wide version: zones within +-radius of the city-tile zone, on the city's island. The zone grid is
        // reached through the public tile map, because World.zone_calculator is internal to the game.
        private static int CountFreeZones(TileZone center, TileIsland island, TerrainRefs refs, int radius)
        {
            if (center == null || island == null)
            {
                return 0;
            }

            int count = 0;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    TileZone zone = ZoneAt(center.x + dx, center.y + dy);
                    if (zone != null && IsFreeLand(zone, refs) && IsOnIsland(zone, island))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static TileZone ZoneAt(int zoneX, int zoneY)
        {
            int half = ZoneSizeTiles / 2;
            return World.world.GetTile(zoneX * ZoneSizeTiles + half, zoneY * ZoneSizeTiles + half)?.zone;
        }

        private static bool IsOnIsland(TileZone zone, TileIsland island)
        {
            List<MapRegion> regions = zone.chunk.regions;
            for (int i = 0; i < regions.Count; i++)
            {
                MapRegion region = regions[i];
                if (region.island == island && region.zones.Contains(zone))
                {
                    return true;
                }
            }

            return false;
        }

        private static RingCounts CountRing(City city, TerrainRefs refs)
        {
            int zones = 0;
            int unclaimed = 0;
            int foreign = 0;
            foreach (TileZone zone in CityInternals.GetNeighbourZones(city))
            {
                // Water, lava and mountain-only zones are not land: they would dilute the shares.
                if (zone.tiles_with_ground <= 0)
                {
                    continue;
                }

                zones++;
                if (zone.city == null)
                {
                    unclaimed += IsFreeLand(zone, refs) ? 1 : 0;
                }
                else if (zone.city != city)
                {
                    foreign++;
                }
            }

            return new RingCounts(zones, unclaimed, foreign);
        }

        private static void CountNeighbourKingdoms(City city, Kingdom own, ref int foreign, ref int hostile)
        {
            foreach (Kingdom other in CityInternals.GetNeighbourKingdoms(city))
            {
                if (other == null || other == own || !other.isAlive())
                {
                    continue;
                }

                foreign++;
                if (own != null && own.isEnemy(other))
                {
                    hostile++;
                }
            }
        }
    }
}
