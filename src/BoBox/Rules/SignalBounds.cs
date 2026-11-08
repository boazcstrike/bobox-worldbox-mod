namespace BoBox.Rules
{
    /// <summary>
    /// Allowed range of each numeric <see cref="SignalRules"/> field. The rules loader clamps a rules
    /// file to these, and the runtime clamps to the same values, so a number the loader accepts is a
    /// number the signal reader runs with.
    /// </summary>
    public static class SignalBounds
    {
        public const int MinCitiesPerFrame = 1;
        public const int MaxCitiesPerFrame = 10;

        public const double MinRefreshSeconds = 5.0;
        public const double MaxRefreshSeconds = 10.0;

        public const double MinCoastCacheSeconds = 30.0;
        public const double MaxCoastCacheSeconds = 3600.0;

        public const int MinCoastMinWaterTiles = 1;
        public const int MaxCoastMinWaterTiles = 1000000;

        public const int MinFreeZoneScanRadius = 0;
        public const int MaxFreeZoneScanRadius = 10;
    }
}
