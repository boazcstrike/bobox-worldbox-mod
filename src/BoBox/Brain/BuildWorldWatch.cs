using System.Reflection;

namespace BoBox.Brain
{
    /// <summary>
    /// Tells the build brain that a new or loaded world replaced the one its per-city maps were filled for.
    /// <see cref="WorldIdentity.HasChanged"/> records what it sees, so a second caller would steal the answer
    /// from the scenario cache; this keeps its own record of MapBox.map_stats (see WorldIdentity for why that
    /// field marks a new world). Also reports a world time that moved backwards, the fallback when the
    /// field is missing. Main thread only.
    /// </summary>
    internal sealed class BuildWorldWatch
    {
        private static readonly FieldInfo MapStatsField = typeof(MapBox).GetField(
            "map_stats", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private object _mapStats;
        private double _lastNow = double.NegativeInfinity;

        /// <summary>True when the world differs from the last call, which also records it. True on the first call.</summary>
        public bool HasChanged(double now)
        {
            bool isChanged = now < _lastNow;
            _lastNow = now;
            if (MapStatsField == null)
            {
                return isChanged;
            }

            object mapStats = MapStatsField.GetValue(World.world);
            if (ReferenceEquals(_mapStats, mapStats))
            {
                return isChanged;
            }

            _mapStats = mapStats;
            return true;
        }
    }
}
