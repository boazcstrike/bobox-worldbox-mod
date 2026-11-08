using System.Reflection;

namespace BoBox.Brain
{
    /// <summary>
    /// Tells a new or loaded world from the one the scenario cache was filled for. World.world and its
    /// city manager are created once (decompiled/MapBox.cs line 322) and never replaced; a new or loaded
    /// map replaces only MapBox.map_stats (`internal`, MapBox.cs line 1037 and SaveManager.cs line 925).
    /// map_stats is bound once; when the field is missing, <see cref="HasChanged"/> stays false and
    /// <see cref="ScenarioCache.Tick"/> falls back to a world time that moves backwards. Main thread only.
    /// </summary>
    internal static class WorldIdentity
    {
        private static readonly FieldInfo MapStatsField = typeof(MapBox).GetField(
            "map_stats", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static object _mapStats;
        private static bool _isMissingLogged;

        /// <summary>True when map_stats differs from the last call, which also records it.</summary>
        public static bool HasChanged()
        {
            if (MapStatsField == null)
            {
                LogMissingOnce();
                return false;
            }

            object mapStats = MapStatsField.GetValue(World.world);
            if (ReferenceEquals(_mapStats, mapStats))
            {
                return false;
            }

            _mapStats = mapStats;
            return true;
        }

        private static void LogMissingOnce()
        {
            if (_isMissingLogged)
            {
                return;
            }

            _isMissingLogged = true;
            Log.Warn("MapBox.map_stats not found, new worlds are detected by world time only");
        }
    }
}
