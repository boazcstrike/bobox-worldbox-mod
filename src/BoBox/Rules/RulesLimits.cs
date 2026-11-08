using System.Collections.Generic;

namespace BoBox.Rules
{
    /// <summary>
    /// Allowed range per numeric field of the rules file, keyed by the field's pattern path: property
    /// names joined with dots, "[]" for an array element, "*" for a dictionary key. A value outside its
    /// range is clamped by <see cref="RulesSanitizer"/>. A field without an entry is not checked.
    /// </summary>
    internal static class RulesLimits
    {
        private const int MaxTiles = 1000000;
        private const int MaxCount = 100000;
        private const double MaxPerUnit = 1000;
        private const double MaxWeight = 100;
        private const double MinMapFactor = 0.1;
        private const double MaxMapFactor = 10;
        private const int MaxMapSize = 1000;
        private const int MaxPriority = 1000;

        internal readonly struct Range
        {
            public double Min { get; }
            public double Max { get; }

            public Range(double min, double max)
            {
                Min = min;
                Max = max;
            }
        }

        private static readonly Range Share = new Range(0, 1);
        private static readonly Range PerUnit = new Range(0, MaxPerUnit);
        private static readonly Range Count = new Range(0, MaxCount);
        private static readonly Range Weight = new Range(0, MaxWeight);
        private static readonly Range IntCount = new Range(0, int.MaxValue);
        private static readonly Range Priority = new Range(0, MaxPriority);

        private static readonly IReadOnlyDictionary<string, Range> Table = new Dictionary<string, Range>
        {
            ["signals.cities_per_frame"] = new Range(SignalBounds.MinCitiesPerFrame, SignalBounds.MaxCitiesPerFrame),
            ["signals.refresh_seconds"] = new Range(SignalBounds.MinRefreshSeconds, SignalBounds.MaxRefreshSeconds),
            ["signals.coast_cache_seconds"] = new Range(SignalBounds.MinCoastCacheSeconds, SignalBounds.MaxCoastCacheSeconds),
            ["signals.coast_min_water_tiles"] = new Range(SignalBounds.MinCoastMinWaterTiles, SignalBounds.MaxCoastMinWaterTiles),
            ["signals.free_zone_scan_radius"] = new Range(SignalBounds.MinFreeZoneScanRadius, SignalBounds.MaxFreeZoneScanRadius),
            ["scenarios.forest_rich.min_trees_per_zone"] = PerUnit,
            ["scenarios.fertile_plains.min_farmable_share"] = Share,
            ["scenarios.fertile_plains.max_rock_share"] = Share,
            ["scenarios.rocky.min_rock_share"] = Share,
            ["scenarios.rocky.min_minerals_per_zone"] = PerUnit,
            ["scenarios.scarce.max_trees_per_zone"] = PerUnit,
            ["scenarios.scarce.max_minerals_per_zone"] = PerUnit,
            ["scenarios.scarce.max_farmable_share"] = Share,
            ["scenarios.scarce.min_harsh_share"] = Share,
            ["scenarios.coastal.max_island_tiles"] = new Range(0, MaxTiles),
            ["scenarios.coastal.min_island_owned_share"] = Share,
            ["scenarios.coastal.min_ocean_share"] = Share,
            ["scenarios.crowded_frontier.min_foreign_ring_share"] = Share,
            ["scenarios.crowded_frontier.max_foreign_city_distance_zones"] = Count,
            ["scenarios.crowded_frontier.min_hostile_neighbours"] = Count,
            ["scenarios.open_frontier.min_unclaimed_ring_share"] = Share,
            ["scenarios.open_frontier.min_free_zones_in_range"] = Count,
            ["pathways[].job_weights.*"] = Weight,
            ["pathways[].capacity_multipliers.jobs.*"] = Weight,
            ["pathways[].capacity_multipliers.buildings.*"] = Weight,
            ["pathways[].building_priority.*"] = Priority,
            ["capacity.jobs.*.min_per_pop"] = PerUnit,
            ["capacity.jobs.*.max_per_zone"] = PerUnit,
            ["capacity.jobs.*.min_floor"] = Count,
            ["capacity.jobs.*.max_ceiling"] = IntCount,
            ["capacity.jobs.*.max_per_pop"] = PerUnit,
            ["capacity.buildings.*.min_per_pop"] = PerUnit,
            ["capacity.buildings.*.max_per_zone"] = PerUnit,
            ["capacity.buildings.*.min_floor"] = Count,
            ["capacity.buildings.*.max_ceiling"] = IntCount,
            ["capacity.buildings.*.max_per_pop"] = PerUnit,
            ["map_scale[].min_size"] = new Range(0, MaxMapSize),
            ["map_scale[].factor"] = new Range(MinMapFactor, MaxMapFactor),
        };

        internal static IEnumerable<string> Keys => Table.Keys;

        internal static bool TryGet(string pattern, out Range range)
        {
            return Table.TryGetValue(pattern, out range);
        }
    }
}
