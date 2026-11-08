namespace BoBox.Rules
{
    /// <summary>
    /// How often city signals are read. Cadence from docs/research/terrain-signals.md: every 5 to 10
    /// seconds per city, round-robin, and the island and coast check at most every 30 seconds.
    /// </summary>
    public sealed class SignalRules
    {
        public const int DefaultCitiesPerFrame = 2;
        public const double DefaultRefreshSeconds = 8.0;
        public const double DefaultCoastCacheSeconds = 30.0;
        public const int DefaultCoastMinWaterTiles = 2500;
        public const int DefaultFreeZoneScanRadius = 9;

        /// <summary>Most cities refreshed in one frame.</summary>
        public int CitiesPerFrame { get; }

        /// <summary>Seconds between two refreshes of the same city.</summary>
        public double RefreshSeconds { get; }

        /// <summary>Seconds a city's island and coast result stays cached.</summary>
        public double CoastCacheSeconds { get; }

        /// <summary>A water body counts as ocean, not a lake, from this many tiles (docks need 2,500).</summary>
        public int CoastMinWaterTiles { get; }

        /// <summary>Zones scanned around the city-tile zone when counting free land.</summary>
        public int FreeZoneScanRadius { get; }

        public SignalRules(
            int citiesPerFrame = DefaultCitiesPerFrame,
            double refreshSeconds = DefaultRefreshSeconds,
            double coastCacheSeconds = DefaultCoastCacheSeconds,
            int coastMinWaterTiles = DefaultCoastMinWaterTiles,
            int freeZoneScanRadius = DefaultFreeZoneScanRadius)
        {
            CitiesPerFrame = citiesPerFrame;
            RefreshSeconds = refreshSeconds;
            CoastCacheSeconds = coastCacheSeconds;
            CoastMinWaterTiles = coastMinWaterTiles;
            FreeZoneScanRadius = freeZoneScanRadius;
        }
    }
}
