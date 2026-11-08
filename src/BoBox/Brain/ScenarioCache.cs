using System;
using System.Collections.Generic;
using System.Linq;
using BoBox.Game;
using BoBox.Planner.Build;
using BoBox.Planner.Capacity;
using BoBox.Planner.Scenario;
using BoBox.Rules;

namespace BoBox.Brain
{
    /// <summary>
    /// Per-city scenario state. <see cref="Tick"/> runs every frame on the main thread and refreshes a
    /// few cities round-robin (<see cref="RefreshBudget"/>: at least rules.signals.cities_per_frame, more when
    /// many cities would miss the refresh interval): signals, classification with pathway hysteresis, limits.
    /// Readers (job step, build step, API) get immutable <see cref="CityScenario"/> entries; the lock
    /// only guards the dictionary.
    ///
    /// Clock: world time (World.getCurWorldTime, map_stats.world_time). It advances with the game speed
    /// and stands still while paused, so a x5 game refreshes 5 times as often per real second and a
    /// paused game does no work. That matches the research cadence "seconds of game time", and the
    /// per-frame cap keeps the cost bounded at any speed. A time that moves backwards (new map) counts as due.
    /// A new or loaded world (a replaced map_stats, or a world time earlier than the last tick) clears every store.
    /// </summary>
    internal static class ScenarioCache
    {
        // Zone count of a mark made after a failure: never equal to a real count, so the city is due again.
        private const int UnknownZones = -1;

        private static readonly object Gate = new object();
        private static readonly Dictionary<long, CityScenario> Entries = new Dictionary<long, CityScenario>();

        // Main thread only (Tick, Prune, Clear): when each city was last tried, successful or not, and its zone count then.
        private static readonly Dictionary<long, RefreshMark> Marks = new Dictionary<long, RefreshMark>();

        private static int _cursor;
        private static double _lastTickAt;
        private static double _lastPruneAt;

        // Raised from any thread by MarkAllDue; Tick takes it on the main thread.
        private static readonly AllDueFlag AllDue = new AllDueFlag();

        /// <summary>
        /// Refreshes up to the tick's budget of cities that are due: never refreshed, refresh_seconds old, or
        /// holding a different zone count than at their last refresh. Starts after the last city served.
        /// A city that throws is marked and skipped, so it never starves the others. The caller runs it only
        /// while CityInternals.IsSignalsAvailable.
        /// </summary>
        public static void Tick()
        {
            // Nothing is read, and the all-due flag stays raised, until the load ends (see LoadWatch).
            if (LoadWatch.IsLoading())
            {
                return;
            }

            // Taken before the rules snapshot, so a reload landing in between never refreshes with the old rules.
            TakeAllDue();

            // One snapshot per tick: a rules reload applies to the next refresh, and the load time stored on
            // each city belongs to the rules that refreshed it.
            ActiveRules.Snapshot state = ActiveRules.Capture();
            SignalRules signalRules = SignalRulesClamp.Of(state.Rules.Signals);
            double now = World.world.getCurWorldTime();

            // Fallback when map_stats cannot be read: world time never moves backwards inside one world.
            // The first tick after a load ends also starts clean: it drops anything read while islands were missing.
            bool hasLoadEnded = LoadWatch.HasJustFinished();
            if (WorldIdentity.HasChanged() || hasLoadEnded || now < _lastTickAt)
            {
                Clear();
                _lastTickAt = now; // nothing elapsed since the load: the first frame gets the floor budget, not the cap
            }

            double elapsed = now - _lastTickAt;
            _lastTickAt = now;
            List<City> cities = World.world.cities.list;
            PruneIfDue(cities, now, signalRules.RefreshSeconds);

            int count = cities.Count;
            int budget = RefreshBudget.For(count, elapsed, signalRules);
            int start = count == 0 ? 0 : _cursor % count;
            int attempted = 0;
            var centers = new CityCenters(); // built on first use: one pass over the cities per tick, not per refresh
            for (int step = 0; step < count && attempted < budget; step++)
            {
                int index = (start + step) % count;
                if (TryRefresh(cities[index], state, signalRules, centers, now))
                {
                    attempted++;
                    _cursor = index + 1;
                }
            }
        }

        /// <summary>The city's entry, or null before its first refresh.</summary>
        public static CityScenario Get(long cityId)
        {
            lock (Gate)
            {
                return Entries.TryGetValue(cityId, out CityScenario entry) ? entry : null;
            }
        }

        /// <summary>A snapshot of every entry, ordered by city id.</summary>
        public static IReadOnlyList<CityScenario> All()
        {
            List<CityScenario> copy;
            lock (Gate)
            {
                copy = new List<CityScenario>(Entries.Values);
            }

            copy.Sort((a, b) => a.CityId.CompareTo(b.CityId));
            return copy.AsReadOnly();
        }

        /// <summary>
        /// Replaces the city's entry with <paramref name="change"/>(current or null), atomically. A null
        /// result leaves the entry as it was. Keep <paramref name="change"/> short: it runs under the lock.
        /// </summary>
        public static void Update(long cityId, Func<CityScenario, CityScenario> change)
        {
            lock (Gate)
            {
                Entries.TryGetValue(cityId, out CityScenario current);
                CityScenario updated = change(current);
                if (updated != null)
                {
                    Entries[cityId] = updated;
                }
            }
        }

        /// <summary>Stores the build step's last choice for the API. Creates a bare entry when the city has none yet.</summary>
        public static void RecordBuildChoice(long cityId, string cityName, BuildChoice choice)
        {
            double now = World.world.getCurWorldTime();
            Update(cityId, current => current != null
                ? current.WithLastBuild(choice, now)
                : new CityScenario(cityId, cityName, null, null, Limits.Unbounded, choice, 0, now));
        }

        /// <summary>Drops every entry and per-city cache of a city not in <paramref name="liveIds"/>, so dead cities leave <see cref="All"/>.</summary>
        private static void Prune(ISet<long> liveIds)
        {
            lock (Gate)
            {
                foreach (long id in Entries.Keys.Where(id => !liveIds.Contains(id)).ToList())
                {
                    Entries.Remove(id);
                }
            }

            foreach (long id in Marks.Keys.Where(id => !liveIds.Contains(id)).ToList())
            {
                Marks.Remove(id);
            }

            ScenarioErrorLog.Forget(liveIds);
            SignalsReader.Forget(liveIds);
        }

        /// <summary>
        /// Makes every city due, so the next ticks refresh each one with the current rules. Entries stay
        /// readable meanwhile. Safe from any thread: it only sets a flag that the next <see cref="Tick"/> takes.
        /// </summary>
        public static void MarkAllDue()
        {
            AllDue.Set();
        }

        /// <summary>Drops every entry and every per-city cache, for a new or loaded world.</summary>
        public static void Clear()
        {
            lock (Gate)
            {
                Entries.Clear();
            }

            Marks.Clear();
            ScenarioErrorLog.Clear();
            SignalsReader.Clear();
            _cursor = 0;
            _lastPruneAt = 0;
        }

        // Takes the flag atomically before it clears the marks, so a MarkAllDue that lands after the take is kept for the next tick.
        private static void TakeAllDue()
        {
            if (AllDue.Take())
            {
                Marks.Clear();
                SignalsReader.Clear(); // the coast cache also holds a reading made under the old rules
            }
        }

        private static void PruneIfDue(List<City> cities, double now, double intervalSeconds)
        {
            if (now >= _lastPruneAt && now - _lastPruneAt < intervalSeconds)
            {
                return;
            }

            _lastPruneAt = now;
            var liveIds = new HashSet<long>();
            for (int i = 0; i < cities.Count; i++)
            {
                if (cities[i].isAlive())
                {
                    liveIds.Add(cities[i].getID());
                }
            }

            Prune(liveIds);
        }

        // True when the city was due and a refresh was attempted, whether or not it worked.
        private static bool TryRefresh(
            City city, ActiveRules.Snapshot state, SignalRules signalRules, CityCenters centers, double now)
        {
            if (!city.isAlive())
            {
                return false;
            }

            long id = city.getID();
            try
            {
                if (!IsDue(id, city, now, signalRules))
                {
                    return false;
                }

                Refresh(city, state, signalRules, centers, now);
                return true;
            }
            catch (Exception ex)
            {
                MarkFailed(id, city, now, ex);
                return true;
            }
        }

        private static bool IsDue(long id, City city, double now, SignalRules rules)
        {
            if (!Marks.TryGetValue(id, out RefreshMark mark) || now < mark.At)
            {
                return true;
            }

            if (now - mark.At >= rules.RefreshSeconds)
            {
                return true;
            }

            // A mark made after a failure has no zone count to compare: it waits refresh_seconds.
            return mark.Zones != UnknownZones && CityInternals.GetZones(city).Count != mark.Zones;
        }

        // The pathway goes through the city's PathwayHold, so a city near a threshold keeps its pathway until
        // another one wins PathwayHold.WinsToSwitch refreshes in a row over refresh_seconds. Only Tick writes the hold.
        private static void Refresh(
            City city, ActiveRules.Snapshot state, SignalRules signalRules, CityCenters centers, double now)
        {
            long id = city.getID();
            ColonyRules rules = state.Rules;

            // Marked before the read, so a city that throws is retried after refresh_seconds, not every frame.
            Marks[id] = new RefreshMark(now, CityInternals.GetZones(city).Count);
            ScenarioSignals signals = SignalsReader.Read(city, signalRules, centers);
            ScenarioResult fresh = ScenarioClassifier.Classify(signals, rules);
            PathwayHold hold = (Get(id)?.Hold ?? PathwayHold.None)
                .Next(fresh.Pathway.Id, rules.Pathways, now, signalRules.RefreshSeconds);
            ScenarioResult result = hold.Apply(fresh, rules.Pathways);
            Limits limits = CityLimits.For(city, signals, result, rules);
            var refreshed = new CityScenario(id, city.name, signals, result, limits, null, now, 0, state.LoadedAtUtc, hold);
            Update(id, current => current != null ? current.With(refreshed) : refreshed);
            ScenarioErrorLog.Succeeded(id);
        }

        // Keeps a mark Refresh already made this tick; else marks the city so it waits refresh_seconds.
        private static void MarkFailed(long id, City city, double now, Exception ex)
        {
            if (!Marks.TryGetValue(id, out RefreshMark mark) || mark.At != now)
            {
                Marks[id] = new RefreshMark(now, UnknownZones);
            }

            ScenarioErrorLog.Report(id, city, ex);
        }
    }
}
