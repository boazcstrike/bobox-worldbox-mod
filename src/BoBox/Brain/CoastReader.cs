using System.Collections.Generic;
using BoBox.Planner.Scenario;
using BoBox.Rules;

namespace BoBox.Brain
{
    /// <summary>
    /// Island and coast check of one city, cached per city. Island and coast change rarely, so a result is
    /// kept for rules.CoastCacheSeconds of world time, unless the city's zone count, its liquid tile count or the
    /// rules' CoastMinWaterTiles changed since. A reading made while islands are being rebuilt is returned but not kept.
    /// Main thread only.
    /// </summary>
    internal static class CoastReader
    {
        private static readonly Dictionary<long, CoastReading> Cache = new Dictionary<long, CoastReading>();

        // Coastal: a city zone with ocean tiles whose chunk holds an ocean-layer island of at least
        // CoastMinWaterTiles (docks need 2,500), so lakes do not count.
        public static CoastReading Read(
            City city, IReadOnlyList<TileZone> zones, TileIsland island, TerrainRefs refs, SignalRules rules)
        {
            double now = World.world.getCurWorldTime();
            long id = city.getID();
            int liquidTiles = LiquidTiles(zones);
            if (Cache.TryGetValue(id, out CoastReading cached)
                && cached.Zones == zones.Count && cached.LiquidTiles == liquidTiles
                && cached.MinWaterTiles == rules.CoastMinWaterTiles
                && now >= cached.At && now - cached.At < rules.CoastCacheSeconds)
            {
                return cached;
            }

            bool isComplete = true;
            bool isCoastal = IsCoastal(zones, refs, rules.CoastMinWaterTiles, ref isComplete);
            var fresh = new CoastReading(
                isCoastal, island?.getTileCount() ?? 0, zones.Count, liquidTiles, rules.CoastMinWaterTiles, now);

            // A terraform clears region.island until the islands are rebuilt (decompiled/MapRegion.cs lines 72-73):
            // that reading says "not coastal, no island", so it must not stick for CoastCacheSeconds.
            if (island != null && isComplete)
            {
                Cache[id] = fresh;
            }
            else
            {
                Cache.Remove(id);
            }

            return fresh;
        }

        /// <summary>Drops the cache of every city not in <paramref name="aliveIds"/>.</summary>
        public static void Forget(ISet<long> aliveIds)
        {
            var dead = new List<long>();
            foreach (long id in Cache.Keys)
            {
                if (!aliveIds.Contains(id))
                {
                    dead.Add(id);
                }
            }

            foreach (long id in dead)
            {
                Cache.Remove(id);
            }
        }

        public static void Clear() => Cache.Clear();

        // tiles_with_liquid is a plain counter on the zone (decompiled/TileZone.cs line 51): cheap to sum.
        private static int LiquidTiles(IReadOnlyList<TileZone> zones)
        {
            int total = 0;
            for (int i = 0; i < zones.Count; i++)
            {
                total += zones[i].tiles_with_liquid;
            }

            return total;
        }

        private static bool IsCoastal(
            IReadOnlyList<TileZone> zones, TerrainRefs refs, int minWaterTiles, ref bool isComplete)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                TileZone zone = zones[i];
                if (zone.tiles_with_liquid > 0 && SignalsReader.HasAnyTile(zone, refs.Ocean)
                    && TouchesBigOcean(zone, minWaterTiles, ref isComplete))
                {
                    return true;
                }
            }

            return false;
        }

        // isComplete turns false when an ocean region of the zone has no island yet (islands mid-rebuild).
        private static bool TouchesBigOcean(TileZone zone, int minWaterTiles, ref bool isComplete)
        {
            List<MapRegion> regions = zone.chunk.regions;
            for (int i = 0; i < regions.Count; i++)
            {
                MapRegion region = regions[i];
                if (region.type != TileLayerType.Ocean || !region.zones.Contains(zone))
                {
                    continue;
                }

                if (region.island == null)
                {
                    isComplete = false;
                }
                else if (region.island.getTileCount() >= minWaterTiles)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
