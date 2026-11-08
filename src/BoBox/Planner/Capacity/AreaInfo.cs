namespace BoBox.Planner.Capacity
{
    /// <summary>How big a city is, in plain numbers. Input to <see cref="CapacityModel.Limits"/>.</summary>
    public sealed class AreaInfo
    {
        /// <summary>Map size in 64-tile units, as in MapSizeAsset.size (standard is 4, bobox_16 is 16).</summary>
        public const int StandardMapSize = 4;

        public int Zones { get; }
        public int LandTiles { get; }
        public int Adults { get; }
        public int Population { get; }
        public int MapSize { get; }

        public AreaInfo(int zones = 0, int landTiles = 0, int adults = 0, int population = 0, int mapSize = StandardMapSize)
        {
            Zones = zones;
            LandTiles = landTiles;
            Adults = adults;
            Population = population;
            MapSize = mapSize;
        }
    }
}
