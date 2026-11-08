using System;
using BoBox.Rules;

namespace BoBox.Planner.Scenario
{
    /// <summary>
    /// Scores one tag at a time from signals and thresholds. Pure, no allocation.
    ///
    /// Score formula. A condition that fails scores 0. A condition that holds scores
    /// <see cref="AtThresholdScore"/> (0.5) exactly on the threshold and rises linearly to 1.0 once the
    /// value is a full threshold past it: 0.5 + 0.5 * clamp01(|value - threshold| / threshold). A zero,
    /// negative or infinite threshold has no scale, so a passing value scores 0.5. A tag with several conditions scores
    /// the weakest condition when all must hold, and the strongest when any one is enough.
    /// So "score > 0" is exactly "the tag matched".
    ///
    /// Boundaries: every Min* threshold is inclusive (value >= min), every Max* threshold is inclusive
    /// (value &lt;= max) except <see cref="FertilePlainsThresholds.MaxRockShare"/> and
    /// <see cref="CoastalThresholds.MaxIslandTiles"/>, which the research words as "below" and which are
    /// exclusive (value &lt; max).
    ///
    /// A city with no zones has no data, so every tag scores 0.
    /// </summary>
    internal static class ScenarioScoring
    {
        public const double AtThresholdScore = 0.5;

        public static double Score(ScenarioTag tag, ScenarioSignals signals, ScenarioThresholds thresholds)
        {
            if (signals.Zones <= 0)
            {
                return 0;
            }

            switch (tag)
            {
                case ScenarioTag.ForestRich: return ForestRich(signals, thresholds.ForestRich);
                case ScenarioTag.FertilePlains: return FertilePlains(signals, thresholds.FertilePlains);
                case ScenarioTag.Rocky: return Rocky(signals, thresholds.Rocky);
                case ScenarioTag.Scarce: return Scarce(signals, thresholds.Scarce);
                case ScenarioTag.Coastal: return Coastal(signals, thresholds.Coastal);
                case ScenarioTag.CrowdedFrontier: return CrowdedFrontier(signals, thresholds.CrowdedFrontier);
                case ScenarioTag.OpenFrontier: return OpenFrontier(signals, thresholds.OpenFrontier);
                default: return 0;
            }
        }

        private static double ForestRich(ScenarioSignals s, ForestRichThresholds t)
        {
            return AtLeast(s.TreesPerZone, t.MinTreesPerZone);
        }

        private static double FertilePlains(ScenarioSignals s, FertilePlainsThresholds t)
        {
            return AllOf(AtLeast(s.FarmableShare, t.MinFarmableShare), Below(s.RockShare, t.MaxRockShare));
        }

        private static double Rocky(ScenarioSignals s, RockyThresholds t)
        {
            return AnyOf(AtLeast(s.RockShare, t.MinRockShare), AtLeast(s.MineralsPerZone, t.MinMineralsPerZone));
        }

        private static double Scarce(ScenarioSignals s, ScarceThresholds t)
        {
            double allLow = AllOf(
                AtMost(s.TreesPerZone, t.MaxTreesPerZone),
                AtMost(s.MineralsPerZone, t.MaxMineralsPerZone),
                AtMost(s.FarmableShare, t.MaxFarmableShare));
            return AnyOf(allLow, AtLeast(s.HarshShare, t.MinHarshShare));
        }

        // The coast flag counts only with enough ocean inside the city (inclusive MinOceanShare), so a city that
        // touches one ocean tile is not coastal. The island branches need a known island (IslandTiles > 0);
        // 0 means the signal was not read.
        private static double Coastal(ScenarioSignals s, CoastalThresholds t)
        {
            double coast = s.IsCoastal && s.OceanShare >= t.MinOceanShare ? AtThresholdScore : 0;
            if (s.IslandTiles <= 0)
            {
                return coast;
            }

            return AnyOf(
                coast,
                Below(s.IslandTiles, t.MaxIslandTiles),
                AtLeast(s.IslandOwnedShare, t.MinIslandOwnedShare));
        }

        private static double CrowdedFrontier(ScenarioSignals s, CrowdedFrontierThresholds t)
        {
            return AnyOf(
                AtLeast(s.RingForeignShare, t.MinForeignRingShare),
                s.HasForeignCity ? AtMost(s.NearestForeignCityZones, t.MaxForeignCityDistanceZones) : 0,
                AtLeast(s.HostileNeighbourKingdoms, t.MinHostileNeighbours));
        }

        private static double OpenFrontier(ScenarioSignals s, OpenFrontierThresholds t)
        {
            return AllOf(
                AtLeast(s.RingUnclaimedShare, t.MinUnclaimedRingShare),
                AtLeast(s.FreeZonesInRange, t.MinFreeZonesInRange));
        }

        /// <summary>Holds when value >= min.</summary>
        internal static double AtLeast(double value, double min)
        {
            return value >= min ? Passing(value - min, min) : 0;
        }

        /// <summary>Holds when value &lt;= max.</summary>
        internal static double AtMost(double value, double max)
        {
            return value <= max ? Passing(max - value, max) : 0;
        }

        /// <summary>Holds when value &lt; max (exclusive).</summary>
        internal static double Below(double value, double max)
        {
            return value < max ? Passing(max - value, max) : 0;
        }

        private static double Passing(double distance, double threshold)
        {
            if (!(threshold > 0) || double.IsInfinity(threshold))
            {
                return AtThresholdScore;
            }

            double fraction = Math.Min(1.0, Math.Max(0.0, distance / threshold));
            return AtThresholdScore + (1.0 - AtThresholdScore) * fraction;
        }

        private static double AllOf(double a, double b)
        {
            return a > 0 && b > 0 ? Math.Min(a, b) : 0;
        }

        private static double AllOf(double a, double b, double c)
        {
            return AllOf(AllOf(a, b), c);
        }

        private static double AnyOf(double a, double b)
        {
            return Math.Max(a, b);
        }

        private static double AnyOf(double a, double b, double c)
        {
            return Math.Max(Math.Max(a, b), c);
        }
    }
}
