using System.Collections.Generic;
using System.Linq;

namespace BoBox.Planner.Build
{
    /// <summary>
    /// Per city, the new building types and the upgrade orders whose last build failed (no free tile, a
    /// vanished upgrade target), kept for <see cref="CooldownSeconds"/> of world time. The build step leaves
    /// those out of the next ticks' candidates, so a blocked pick cannot starve the ones the planner would
    /// pick after it. At most one entry per key per city, and at most <see cref="MaxEntriesPerCity"/> per city:
    /// expired entries are dropped on each new failure, then the oldest go if the cap is still exceeded.
    /// Pure: the world time is passed in as seconds, so it runs outside the game.
    /// </summary>
    public sealed class BuildFailureMemory
    {
        public const double CooldownSeconds = 30.0;
        public const int MaxEntriesPerCity = 64;

        // Id of the owning city, then failure key, to the world time of that key's last failed build.
        private readonly Dictionary<long, Dictionary<string, double>> _failedAt =
            new Dictionary<long, Dictionary<string, double>>();

        public int CityCount => _failedAt.Count;

        /// <summary>Entries held for one city, expired ones included until the next failure drops them.</summary>
        public int EntryCount(long cityId)
        {
            return _failedAt.TryGetValue(cityId, out Dictionary<string, double> keys) ? keys.Count : 0;
        }

        /// <summary>
        /// A new building is keyed by its type, an upgrade by "U-" plus its order id. So a failed upgrade blocks
        /// neither new buildings of the type nor another upgrade order of the same type (hall to hall_2 failing
        /// leaves another upgrade order open).
        /// </summary>
        public static string KeyOf(BuildCandidate candidate)
        {
            return KeyOf(candidate.IsUpgrade, candidate.OrderId, candidate.BuildingType);
        }

        /// <summary>The same key from the parts of a build order, so a caller can ask before it builds a candidate.</summary>
        public static string KeyOf(bool isUpgrade, string orderId, string buildingType)
        {
            return isUpgrade ? "U-" + orderId : buildingType;
        }

        public bool HasFailedRecently(long cityId, BuildCandidate candidate, double now)
        {
            return HasFailedRecently(cityId, KeyOf(candidate), now);
        }

        public bool HasFailedRecently(long cityId, string key, double now)
        {
            return _failedAt.TryGetValue(cityId, out Dictionary<string, double> keys)
                && keys.TryGetValue(key, out double failedAt)
                && IsCoolingDown(failedAt, now);
        }

        /// <summary>Replaces the city's map with a new one: the still-cooling entries plus these failures.</summary>
        public void Remember(long cityId, IReadOnlyList<BuildCandidate> failed, double now)
        {
            if (failed.Count == 0)
            {
                return;
            }

            Dictionary<string, double> next = _failedAt.TryGetValue(cityId, out Dictionary<string, double> old)
                ? old.Where(entry => IsCoolingDown(entry.Value, now)).ToDictionary(entry => entry.Key, entry => entry.Value)
                : new Dictionary<string, double>();
            foreach (BuildCandidate candidate in failed)
            {
                next[KeyOf(candidate)] = now;
            }

            _failedAt[cityId] = next.Count <= MaxEntriesPerCity
                ? next
                : next.OrderByDescending(entry => entry.Value)
                    .Take(MaxEntriesPerCity)
                    .ToDictionary(entry => entry.Key, entry => entry.Value);
        }

        /// <summary>Forgets every city, for a new or loaded world whose city ids can repeat the old ones.</summary>
        public void Clear()
        {
            _failedAt.Clear();
        }

        public void Prune(HashSet<long> liveCityIds)
        {
            foreach (long id in _failedAt.Keys.Where(id => !liveCityIds.Contains(id)).ToList())
            {
                _failedAt.Remove(id);
            }
        }

        // A world time that went backwards (a load) counts as expired, like the build timer.
        private static bool IsCoolingDown(double failedAt, double now)
        {
            return now >= failedAt && now - failedAt < CooldownSeconds;
        }
    }
}
