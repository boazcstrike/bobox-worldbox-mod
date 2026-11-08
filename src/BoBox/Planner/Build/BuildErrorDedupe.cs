using System.Collections.Generic;
using System.Linq;

namespace BoBox.Planner.Build
{
    /// <summary>
    /// Per city and per message, the world time it was last logged, so a failure that repeats every build tick is
    /// logged at most once per <see cref="WindowSeconds"/>. A clean tick does not reset it: an error that comes and
    /// goes stays quiet, and a recurrence after the window logs again. The caller injects the world time.
    /// At most <see cref="MaxMessagesPerCity"/> messages per city: past that the oldest one is forgotten.
    /// Pure: no game types, so it runs outside the game.
    /// </summary>
    public sealed class BuildErrorDedupe
    {
        public const int MaxMessagesPerCity = 8;
        public const double WindowSeconds = 300;

        private readonly Dictionary<long, Dictionary<string, double>> _seen = new Dictionary<long, Dictionary<string, double>>();

        public int CityCount => _seen.Count;

        /// <summary>
        /// True when this city has not reported this message within the window before <paramref name="now"/>; records it.
        /// A world time that went backwards (a load) counts as outside the window.
        /// </summary>
        public bool ShouldLog(long cityId, string message, double now)
        {
            if (!_seen.TryGetValue(cityId, out Dictionary<string, double> messages))
            {
                messages = new Dictionary<string, double>();
                _seen[cityId] = messages;
            }

            if (messages.TryGetValue(message, out double last) && now >= last && now - last < WindowSeconds)
            {
                return false;
            }

            if (!messages.ContainsKey(message) && messages.Count >= MaxMessagesPerCity)
            {
                messages.Remove(messages.OrderBy(pair => pair.Value).First().Key);
            }

            messages[message] = now;
            return true;
        }

        /// <summary>Forgets every city, for a new or loaded world whose city ids can repeat the old ones.</summary>
        public void Clear()
        {
            _seen.Clear();
        }

        public void Prune(HashSet<long> liveCityIds)
        {
            foreach (long id in _seen.Keys.Where(id => !liveCityIds.Contains(id)).ToList())
            {
                _seen.Remove(id);
            }
        }
    }
}
