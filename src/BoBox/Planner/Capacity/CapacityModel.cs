using System;
using System.Collections.Generic;
using BoBox.Planner.Scenario;
using BoBox.Rules;

namespace BoBox.Planner.Capacity
{
    /// <summary>
    /// Turns a city's area and population into minimum and maximum counts per job and per building
    /// type, from <see cref="ColonyRules.Capacity"/>, the pathway's multipliers and the map scale. Pure.
    /// </summary>
    /// <remarks>
    /// The algorithm, for every id that has a <see cref="CapacityBase"/> in the rules (ids without one
    /// stay unbounded):
    /// <list type="number">
    /// <item>Land = land tiles / 64 (one zone is 8 x 8 tiles), or the zone count when land tiles are
    /// unknown (0). Land rather than raw zones, so a half-water coastal zone counts as half.</item>
    /// <item>Scale = <see cref="ColonyRules.ScaleForMapSize"/> of the world's map size. Multiplier =
    /// the pathway's <see cref="CapacityMultipliers"/> entry for the id (negative counts as 0, NaN or
    /// infinity as 1).</item>
    /// <item>Max = min(ceiling, area max). Ceiling = max(floor(MaxCeiling x scale x multiplier),
    /// floor(MaxPerPop x population x multiplier)), so a populous city outgrows the fixed ceiling; no
    /// ceiling for <see cref="CapacityBase.NoCeiling"/>. Area max = ceil(MaxPerZone x land x scale x
    /// multiplier), rounded up so any positive rate allows one slot as soon as the city has land.
    /// MaxPerZone &lt;= 0 means the area sets no maximum and only the ceiling applies.</item>
    /// <item>Min = max(MinFloor, floor(MinPerPop x population x multiplier)). The map scale does not
    /// touch the minimum: it grows with people, not land. A job minimum never exceeds the adults,
    /// because only adults can work.</item>
    /// <item>When min &gt; max, the max wins (min = max). Land is a hard limit: a city cannot place
    /// more fields or houses than it has room for, while the minimum is only a wish.</item>
    /// </list>
    /// So 0 &lt;= min &lt;= max always holds, the maximum grows with land and map size (its ceiling also
    /// with population), and the minimum grows with population.
    /// </remarks>
    public static class CapacityModel
    {
        public const double TilesPerZone = 64.0;

        // Absorbs float error, so 0.1 x 30 counts as 3, not 4 after rounding up.
        private const double RoundingSlack = 1e-9;

        public static Limits Limits(AreaInfo area, Pathway pathway, ColonyRules rules)
        {
            if (area == null)
            {
                return Capacity.Limits.Unbounded;
            }

            ColonyRules active = rules ?? ColonyRules.Default;
            CapacityMultipliers multipliers = (pathway ?? Pathway.Neutral).CapacityMultipliers;
            var size = new CitySize(
                LandZones(area),
                Math.Max(0, active.ScaleForMapSize(area.MapSize)),
                Math.Max(0, area.Population));
            int adults = Math.Max(0, area.Adults);

            return new Limits(
                Ranges(active.Capacity.Jobs, multipliers.ForJob, size, adults),
                Ranges(active.Capacity.Buildings, multipliers.ForBuilding, size, int.MaxValue));
        }

        private static IReadOnlyDictionary<string, CountRange> Ranges(
            IReadOnlyDictionary<string, CapacityBase> rules, Func<string, double> multiplierFor, CitySize size, int minCap)
        {
            var ranges = new Dictionary<string, CountRange>();
            foreach (KeyValuePair<string, CapacityBase> pair in rules)
            {
                ranges[pair.Key] = Range(pair.Value, multiplierFor(pair.Key), size, minCap);
            }

            return ranges;
        }

        private static CountRange Range(CapacityBase rule, double multiplier, CitySize size, int minCap)
        {
            double factor = Factor(multiplier);
            int max = Max(rule, factor, size);
            int min = Math.Min(Math.Min(Min(rule, factor, size), minCap), max);
            return new CountRange(min, max);
        }

        private static int Max(CapacityBase rule, double factor, CitySize size)
        {
            int ceiling = Ceiling(rule, factor, size);
            if (rule.MaxPerZone <= 0)
            {
                return ceiling;
            }

            return Math.Min(ceiling, RoundUp(rule.MaxPerZone * size.LandZones * size.MapScale * factor));
        }

        private static int Ceiling(CapacityBase rule, double factor, CitySize size)
        {
            if (rule.MaxCeiling == CapacityBase.NoCeiling)
            {
                return int.MaxValue;
            }

            int fixedCeiling = RoundDown(Math.Max(0, rule.MaxCeiling) * size.MapScale * factor);
            int byPopulation = RoundDown(Math.Max(0, rule.MaxPerPop) * size.Population * factor);
            return Math.Max(fixedCeiling, byPopulation);
        }

        private static int Min(CapacityBase rule, double factor, CitySize size)
        {
            int byPopulation = RoundDown(Math.Max(0, rule.MinPerPop) * size.Population * factor);
            return Math.Max(Math.Max(0, rule.MinFloor), byPopulation);
        }

        // A NaN or +Infinity multiplier is a broken scenario file: treat it as neutral, not as 0 or
        // unbounded. Negative, including -Infinity, counts as 0.
        private static double Factor(double multiplier)
        {
            if (double.IsNaN(multiplier) || double.IsPositiveInfinity(multiplier))
            {
                return CapacityMultipliers.Neutral;
            }

            return Math.Max(0, multiplier);
        }

        private static double LandZones(AreaInfo area)
        {
            return area.LandTiles > 0 ? area.LandTiles / TilesPerZone : Math.Max(0, area.Zones);
        }

        private static int RoundUp(double value) => ToCount(Math.Ceiling(value - RoundingSlack));

        private static int RoundDown(double value) => ToCount(Math.Floor(value + RoundingSlack));

        private static int ToCount(double value)
        {
            if (double.IsNaN(value) || value <= 0)
            {
                return 0;
            }

            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        private readonly struct CitySize
        {
            public double LandZones { get; }
            public double MapScale { get; }
            public int Population { get; }

            public CitySize(double landZones, double mapScale, int population)
            {
                LandZones = landZones;
                MapScale = mapScale;
                Population = population;
            }
        }
    }
}
