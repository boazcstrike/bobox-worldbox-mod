using System;
using BoBox.Rules;

namespace BoBox.Brain
{
    /// <summary>
    /// Local limits on the signals rules, as defense in depth: the rules loader clamps to the same
    /// <see cref="SignalBounds"/>. The clamped copy is rebuilt only when the rules object changes, so a
    /// tick allocates nothing.
    /// </summary>
    internal static class SignalRulesClamp
    {
        private static SignalRules _raw;
        private static SignalRules _clamped;

        public static SignalRules Of(SignalRules raw)
        {
            if (ReferenceEquals(raw, _raw))
            {
                return _clamped;
            }

            _clamped = new SignalRules(
                citiesPerFrame: Clamp(raw.CitiesPerFrame, SignalBounds.MinCitiesPerFrame, SignalBounds.MaxCitiesPerFrame),
                refreshSeconds: Clamp(raw.RefreshSeconds, SignalBounds.MinRefreshSeconds, SignalBounds.MaxRefreshSeconds),
                coastCacheSeconds: Clamp(raw.CoastCacheSeconds, SignalBounds.MinCoastCacheSeconds, SignalBounds.MaxCoastCacheSeconds),
                coastMinWaterTiles: Clamp(raw.CoastMinWaterTiles, SignalBounds.MinCoastMinWaterTiles, SignalBounds.MaxCoastMinWaterTiles),
                freeZoneScanRadius: Clamp(raw.FreeZoneScanRadius, SignalBounds.MinFreeZoneScanRadius, SignalBounds.MaxFreeZoneScanRadius));
            _raw = raw;
            return _clamped;
        }

        // Math.Clamp does not exist on net472. NaN falls to the minimum.
        private static double Clamp(double value, double min, double max) =>
            value >= min ? (value <= max ? value : max) : min;

        private static int Clamp(int value, int min, int max) => Math.Min(max, Math.Max(min, value));
    }
}
