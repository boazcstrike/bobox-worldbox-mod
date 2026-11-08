namespace BoBox.Rules
{
    /// <summary>
    /// Thresholds per scenario tag. Starting values are the guesses in docs/research/terrain-signals.md,
    /// to be tuned through the rules file. Shares are fractions of the city's land tiles (0..1) unless noted.
    /// </summary>
    public sealed class ScenarioThresholds
    {
        public ForestRichThresholds ForestRich { get; }
        public FertilePlainsThresholds FertilePlains { get; }
        public RockyThresholds Rocky { get; }
        public ScarceThresholds Scarce { get; }
        public CoastalThresholds Coastal { get; }
        public CrowdedFrontierThresholds CrowdedFrontier { get; }
        public OpenFrontierThresholds OpenFrontier { get; }

        public ScenarioThresholds(
            ForestRichThresholds forestRich = null,
            FertilePlainsThresholds fertilePlains = null,
            RockyThresholds rocky = null,
            ScarceThresholds scarce = null,
            CoastalThresholds coastal = null,
            CrowdedFrontierThresholds crowdedFrontier = null,
            OpenFrontierThresholds openFrontier = null)
        {
            ForestRich = forestRich ?? new ForestRichThresholds();
            FertilePlains = fertilePlains ?? new FertilePlainsThresholds();
            Rocky = rocky ?? new RockyThresholds();
            Scarce = scarce ?? new ScarceThresholds();
            Coastal = coastal ?? new CoastalThresholds();
            CrowdedFrontier = crowdedFrontier ?? new CrowdedFrontierThresholds();
            OpenFrontier = openFrontier ?? new OpenFrontierThresholds();
        }
    }

    public sealed class ForestRichThresholds
    {
        public double MinTreesPerZone { get; }

        public ForestRichThresholds(double minTreesPerZone = 2.0)
        {
            MinTreesPerZone = minTreesPerZone;
        }
    }

    public sealed class FertilePlainsThresholds
    {
        public double MinFarmableShare { get; }

        /// <summary>Hills plus mountain share must stay below this.</summary>
        public double MaxRockShare { get; }

        public FertilePlainsThresholds(double minFarmableShare = 0.6, double maxRockShare = 0.1)
        {
            MinFarmableShare = minFarmableShare;
            MaxRockShare = maxRockShare;
        }
    }

    /// <summary>Rocky when either the rock share or the minerals per zone reaches its minimum.</summary>
    public sealed class RockyThresholds
    {
        public double MinRockShare { get; }
        public double MinMineralsPerZone { get; }

        public RockyThresholds(double minRockShare = 0.25, double minMineralsPerZone = 1.0)
        {
            MinRockShare = minRockShare;
            MinMineralsPerZone = minMineralsPerZone;
        }
    }

    /// <summary>
    /// Scarce when trees, minerals and farmable land are all at or under their maximums, or when
    /// harsh ground (hostile biome, snow, sand) reaches <see cref="MinHarshShare"/>.
    /// </summary>
    public sealed class ScarceThresholds
    {
        public double MaxTreesPerZone { get; }
        public double MaxMineralsPerZone { get; }
        public double MaxFarmableShare { get; }
        public double MinHarshShare { get; }

        public ScarceThresholds(
            double maxTreesPerZone = 0.5,
            double maxMineralsPerZone = 0.25,
            double maxFarmableShare = 0.2,
            double minHarshShare = 0.4)
        {
            MaxTreesPerZone = maxTreesPerZone;
            MaxMineralsPerZone = maxMineralsPerZone;
            MaxFarmableShare = maxFarmableShare;
            MinHarshShare = minHarshShare;
        }
    }

    /// <summary>
    /// One tag covers coast and island: coastal is true, or the island is small, or the city owns
    /// most of its island. <see cref="MinOceanShare"/> is the share of the city's own tiles (land plus
    /// ocean, <see cref="BoBox.Planner.Scenario.ScenarioSignals.OceanShare"/>) that must be ocean water
    /// for the coast flag to count; the island branches do not use it.
    /// </summary>
    public sealed class CoastalThresholds
    {
        public int MaxIslandTiles { get; }
        public double MinIslandOwnedShare { get; }
        public double MinOceanShare { get; }

        public CoastalThresholds(int maxIslandTiles = 2500, double minIslandOwnedShare = 0.5, double minOceanShare = 0.1)
        {
            MaxIslandTiles = maxIslandTiles;
            MinIslandOwnedShare = minIslandOwnedShare;
            MinOceanShare = minOceanShare;
        }
    }

    /// <summary>Crowded when any one of the three holds. Ring shares are fractions of the ring zones.</summary>
    public sealed class CrowdedFrontierThresholds
    {
        public double MinForeignRingShare { get; }
        public int MaxForeignCityDistanceZones { get; }
        public int MinHostileNeighbours { get; }

        public CrowdedFrontierThresholds(
            double minForeignRingShare = 0.3,
            int maxForeignCityDistanceZones = 6,
            int minHostileNeighbours = 1)
        {
            MinForeignRingShare = minForeignRingShare;
            MaxForeignCityDistanceZones = maxForeignCityDistanceZones;
            MinHostileNeighbours = minHostileNeighbours;
        }
    }

    /// <summary>Open when both hold.</summary>
    public sealed class OpenFrontierThresholds
    {
        public double MinUnclaimedRingShare { get; }
        public int MinFreeZonesInRange { get; }

        public OpenFrontierThresholds(double minUnclaimedRingShare = 0.5, int minFreeZonesInRange = 10)
        {
            MinUnclaimedRingShare = minUnclaimedRingShare;
            MinFreeZonesInRange = minFreeZonesInRange;
        }
    }
}
