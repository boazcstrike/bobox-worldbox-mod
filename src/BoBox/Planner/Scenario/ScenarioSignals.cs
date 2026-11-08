using System;

namespace BoBox.Planner.Scenario
{
    /// <summary>
    /// Plain numbers about one city's land and neighbours, read from zone counts by Brain.SignalsReader.
    /// Field set from docs/research/terrain-signals.md ("Signal set"). Tile counts are per layer: a tile
    /// counts once in its main-type sums and once in its top-type sums, never twice in one sum.
    /// </summary>
    public sealed class ScenarioSignals
    {
        /// <summary>Value of <see cref="NearestForeignCityZones"/> when no foreign city exists.</summary>
        public const int NoForeignCity = int.MaxValue;

        public static readonly ScenarioSignals Empty = new ScenarioSignals();

        // Area.
        public int Zones { get; }
        public int LandTiles { get; }

        // Resource buildings, from zone countBuildingsType.
        public int Trees { get; }
        public int Minerals { get; }
        public int Bushes { get; }
        public int Plants { get; }
        public int Hives { get; }

        // Ground, from zone tile types.
        public int FarmableTiles { get; }
        public int GrassTiles { get; }
        public int FieldTiles { get; }
        public int HillsTiles { get; }

        /// <summary>Mountains plus summit.</summary>
        public int MountainTiles { get; }

        public int SandTiles { get; }
        public int SnowTiles { get; }
        public int LavaTiles { get; }

        /// <summary>Tiles whose biome restricts building (only_allowed_to_build_with_tag).</summary>
        public int HostileBiomeTiles { get; }

        /// <summary>
        /// Tiles that are harsh in any layer (sand main type, snow or hostile-biome top type), each tile counted once.
        /// Brain.TerrainTotals counts per tile, because the per-kind sums above overlap and can sit on different tiles.
        /// </summary>
        public int HarshTiles { get; }

        public int OceanTiles { get; }

        /// <summary>Most common top-type biome asset id, or empty.</summary>
        public string DominantBiome { get; }

        // Coast and island.
        public bool IsCoastal { get; }
        public int IslandTiles { get; }

        /// <summary>The city's own tiles as a share of the island (0..1).</summary>
        public double IslandOwnedShare { get; }

        // Ring (neighbour_zones) and range.
        public int RingZones { get; }
        public int RingUnclaimedZones { get; }
        public int RingForeignZones { get; }
        public int FreeZonesInRange { get; }

        // Neighbours.
        public int NeighbourCities { get; }
        public int ForeignNeighbourKingdoms { get; }
        public int HostileNeighbourKingdoms { get; }

        /// <summary>Distance in zones to the nearest city of another kingdom; <see cref="NoForeignCity"/> when none.</summary>
        public int NearestForeignCityZones { get; }

        public bool IsAtWar { get; }

        // Status.
        public int Population { get; }
        public int MaxHouses { get; }

        public ScenarioSignals(
            int zones = 0, int landTiles = 0,
            int trees = 0, int minerals = 0, int bushes = 0, int plants = 0, int hives = 0,
            int farmableTiles = 0, int grassTiles = 0, int fieldTiles = 0,
            int hillsTiles = 0, int mountainTiles = 0, int sandTiles = 0, int snowTiles = 0,
            int lavaTiles = 0, int hostileBiomeTiles = 0, int harshTiles = 0, int oceanTiles = 0,
            string dominantBiome = null,
            bool isCoastal = false, int islandTiles = 0, double islandOwnedShare = 0,
            int ringZones = 0, int ringUnclaimedZones = 0, int ringForeignZones = 0, int freeZonesInRange = 0,
            int neighbourCities = 0, int foreignNeighbourKingdoms = 0, int hostileNeighbourKingdoms = 0,
            int nearestForeignCityZones = NoForeignCity,
            bool isAtWar = false,
            int population = 0, int maxHouses = 0)
        {
            Zones = zones;
            LandTiles = landTiles;
            Trees = trees;
            Minerals = minerals;
            Bushes = bushes;
            Plants = plants;
            Hives = hives;
            FarmableTiles = farmableTiles;
            GrassTiles = grassTiles;
            FieldTiles = fieldTiles;
            HillsTiles = hillsTiles;
            MountainTiles = mountainTiles;
            SandTiles = sandTiles;
            SnowTiles = snowTiles;
            LavaTiles = lavaTiles;
            HostileBiomeTiles = hostileBiomeTiles;
            HarshTiles = harshTiles;
            OceanTiles = oceanTiles;
            DominantBiome = dominantBiome ?? string.Empty;
            IsCoastal = isCoastal;
            IslandTiles = islandTiles;
            IslandOwnedShare = islandOwnedShare;
            RingZones = ringZones;
            RingUnclaimedZones = ringUnclaimedZones;
            RingForeignZones = ringForeignZones;
            FreeZonesInRange = freeZonesInRange;
            NeighbourCities = neighbourCities;
            ForeignNeighbourKingdoms = foreignNeighbourKingdoms;
            HostileNeighbourKingdoms = hostileNeighbourKingdoms;
            NearestForeignCityZones = nearestForeignCityZones;
            IsAtWar = isAtWar;
            Population = population;
            MaxHouses = maxHouses;
        }

        /// <summary>False when <see cref="NearestForeignCityZones"/> is negative or the <see cref="NoForeignCity"/> sentinel. Check this before any arithmetic or comparison on the distance.</summary>
        public bool HasForeignCity => NearestForeignCityZones >= 0 && NearestForeignCityZones != NoForeignCity;

        // Derived ratios. Each is 0 when its denominator is 0, so an empty city never divides by zero.
        public double TreesPerZone => PerZone(Trees);
        public double MineralsPerZone => PerZone(Minerals);
        public double FarmableShare => LandShare(FarmableTiles);
        public double RockShare => SolidShare(NonNegative(HillsTiles) + NonNegative(MountainTiles));
        public double HarshShare => Math.Min(1.0, SolidShare(NonNegative(HarshTiles)));
        public double RingForeignShare => Ratio(RingForeignZones, RingZones);
        public double RingUnclaimedShare => Ratio(RingUnclaimedZones, RingZones);

        /// <summary>Ocean tiles as a share of the city's land plus ocean tiles (0..1).</summary>
        public double OceanShare => Ratio(OceanTiles, NonNegative(LandTiles) + NonNegative(OceanTiles));

        /// <summary>
        /// Ground tiles plus mountain and summit tiles. <see cref="LandTiles"/> counts tiles_with_ground, and hills
        /// have ground but mountains and summit (Block layer) do not, so the rock and harsh shares divide by this
        /// sum to stay within 0..1 (TileLibrary.cs: hills ground = true; mountains, summit layer_type = Block).
        /// </summary>
        public int SolidTiles => NonNegative(LandTiles) + NonNegative(MountainTiles);

        // A negative count is bad input; clamp each term so it cannot cancel a real one in a sum.
        private static int NonNegative(int count) => Math.Max(0, count);

        private double PerZone(int count) => Ratio(count, Zones);

        private double LandShare(int tiles) => Ratio(tiles, LandTiles);

        private double SolidShare(int tiles) => Ratio(tiles, SolidTiles);

        private static double Ratio(int part, int whole) => whole <= 0 ? 0 : Math.Max(0, part) / (double)whole;
    }
}
