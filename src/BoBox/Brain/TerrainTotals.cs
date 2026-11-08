using System.Collections.Generic;

namespace BoBox.Brain
{
    /// <summary>
    /// Static tile-type references of the game, cached once (docs/research/terrain-signals.md, "Terrain
    /// facts"). Only build after the game has loaded its tile libraries.
    /// </summary>
    internal sealed class TerrainRefs
    {
        public readonly TileType Hills = TileLibrary.hills;
        public readonly TileType Mountains = TileLibrary.mountains;
        public readonly TileType Summit = TileLibrary.summit;
        public readonly TileType Sand = TileLibrary.sand;
        public readonly TopTileType Field = TopTileLibrary.field;

        /// <summary>Deep, close and shallow ocean. A lake uses the same types.</summary>
        public readonly TileTypeBase[] Ocean =
        {
            TileLibrary.deep_ocean, TileLibrary.close_ocean, TileLibrary.shallow_waters,
        };

        public readonly TileTypeBase[] Lava =
        {
            TileLibrary.lava0, TileLibrary.lava1, TileLibrary.lava2, TileLibrary.lava3,
        };

        /// <summary>Snow, permafrost, frozen and ice are top types.</summary>
        public readonly HashSet<TileTypeBase> Snow = new HashSet<TileTypeBase>
        {
            TopTileLibrary.snow_sand, TopTileLibrary.snow_hills, TopTileLibrary.snow_block,
            TopTileLibrary.snow_summit, TopTileLibrary.permafrost_low, TopTileLibrary.permafrost_high,
            TopTileLibrary.frozen_low, TopTileLibrary.frozen_high, TopTileLibrary.ice,
        };

        public bool IsOcean(TileTypeBase type) => System.Array.IndexOf(Ocean, type) >= 0;

        public bool IsLava(TileTypeBase type) => System.Array.IndexOf(Lava, type) >= 0;
    }

    /// <summary>
    /// Running sums over one city's zones. A tile sits in the zone under its main type and under its top
    /// type, so each layer is summed on its own and never added to the other.
    /// </summary>
    internal sealed class TerrainTotals
    {
        public int LandTiles;
        public int Trees;
        public int Minerals;
        public int Bushes;
        public int Plants;
        public int Hives;
        public int FarmableMain;
        public int FarmableTop;
        public int Grass;
        public int Field;
        public int Hills;
        public int Mountain;
        public int Sand;
        public int Snow;
        public int Lava;
        public int HostileBiome;

        /// <summary>Tiles harsh in any layer (sand main, snow or hostile-biome top), each tile counted once.</summary>
        public int HarshTiles;

        public int Ocean;

        private readonly Dictionary<string, int> _biomeTiles = new Dictionary<string, int>();
        private readonly HashSet<WorldTile> _zoneHarshTiles = new HashSet<WorldTile>();

        public void AddZone(TileZone zone, TerrainRefs refs)
        {
            LandTiles += zone.tiles_with_ground;
            Trees += zone.countBuildingsType(BuildingList.Trees);
            Minerals += zone.countBuildingsType(BuildingList.Minerals);
            Bushes += zone.countBuildingsType(BuildingList.Food);
            Plants += zone.countBuildingsType(BuildingList.Flora);
            Hives += zone.countBuildingsType(BuildingList.Hives);

            _zoneHarshTiles.Clear();
            foreach (KeyValuePair<TileTypeBase, HashSet<WorldTile>> pair in zone.getTileTypes())
            {
                int count = pair.Value.Count;
                if (count == 0)
                {
                    continue;
                }

                bool isTop = pair.Key is TopTileType;
                if (isTop)
                {
                    AddTop(pair.Key, count, refs);
                }
                else
                {
                    AddMain(pair.Key, count, refs);
                }

                if (isTop ? IsHarshTop(pair.Key, refs) : pair.Key == refs.Sand)
                {
                    _zoneHarshTiles.UnionWith(pair.Value);
                }
            }

            // A tile sits in one zone, so the per-zone union counts each harsh tile exactly once.
            HarshTiles += _zoneHarshTiles.Count;
        }

        /// <summary>The top-layer biome asset id with the most tiles, or empty.</summary>
        public string DominantBiome()
        {
            string best = string.Empty;
            int bestCount = 0;
            foreach (KeyValuePair<string, int> pair in _biomeTiles)
            {
                if (pair.Value > bestCount)
                {
                    best = pair.Key;
                    bestCount = pair.Value;
                }
            }

            return best;
        }

        private void AddMain(TileTypeBase type, int count, TerrainRefs refs)
        {
            if (type.can_be_farm)
            {
                FarmableMain += count;
            }

            if (type == refs.Hills)
            {
                Hills += count;
            }
            else if (type == refs.Mountains || type == refs.Summit)
            {
                Mountain += count;
            }
            else if (type == refs.Sand)
            {
                Sand += count;
            }
            else if (refs.IsOcean(type))
            {
                Ocean += count;
            }
            else if (refs.IsLava(type))
            {
                Lava += count;
            }
        }

        private static bool IsHostile(TileTypeBase type) => !string.IsNullOrEmpty(type.only_allowed_to_build_with_tag);

        private static bool IsHarshTop(TileTypeBase type, TerrainRefs refs) => refs.Snow.Contains(type) || IsHostile(type);

        private void AddTop(TileTypeBase type, int count, TerrainRefs refs)
        {
            if (type.can_be_farm)
            {
                FarmableTop += count;
            }

            if (type.grass)
            {
                Grass += count;
            }

            if (type == refs.Field)
            {
                Field += count;
            }

            if (refs.Snow.Contains(type))
            {
                Snow += count;
            }

            if (IsHostile(type))
            {
                HostileBiome += count;
            }

            if (type.biome_asset != null)
            {
                string biome = type.biome_asset.id;
                _biomeTiles[biome] = (_biomeTiles.TryGetValue(biome, out int sum) ? sum : 0) + count;
            }
        }
    }

    /// <summary>One city's cached island and coast result, taken at world time <see cref="At"/> with <see cref="Zones"/> zones.</summary>
    internal sealed class CoastReading
    {
        public readonly bool IsCoastal;
        public readonly int IslandTiles;
        public readonly int Zones;

        /// <summary>Liquid tiles over the city's zones when read: a terraform changes it, so it keys the cache.</summary>
        public readonly int LiquidTiles;

        /// <summary>The rules.CoastMinWaterTiles this reading was made with: a rules reload invalidates it.</summary>
        public readonly int MinWaterTiles;

        public readonly double At;

        public CoastReading(bool isCoastal, int islandTiles, int zones, int liquidTiles, int minWaterTiles, double at)
        {
            IsCoastal = isCoastal;
            IslandTiles = islandTiles;
            Zones = zones;
            LiquidTiles = liquidTiles;
            MinWaterTiles = minWaterTiles;
            At = at;
        }
    }

    /// <summary>Counts over a city's neighbour_zones ring.</summary>
    internal readonly struct RingCounts
    {
        public readonly int Zones;
        public readonly int Unclaimed;
        public readonly int Foreign;

        public RingCounts(int zones, int unclaimed, int foreign)
        {
            Zones = zones;
            Unclaimed = unclaimed;
            Foreign = foreign;
        }
    }
}
