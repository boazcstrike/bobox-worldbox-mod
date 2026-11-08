using System;
using System.Collections.Generic;
using System.Linq;
using BoBox.Planner;

namespace BoBox.Brain
{
    /// <summary>
    /// Shared brain state: the on/off switch, the last plan and last error per city, and the decision log.
    /// Written by <see cref="BoBoxCitizenTasks"/> on the main thread; read by API handlers.
    /// </summary>
    internal static class ColonyBrain
    {
        public const int LogCapacity = 200;

        private static readonly object Gate = new object();
        private static readonly Dictionary<long, JobPlan> LastPlans = new Dictionary<long, JobPlan>();
        private static readonly Dictionary<long, string> LastErrors = new Dictionary<long, string>();
        private static volatile bool _enabled = true;

        public static DecisionLog Decisions { get; } = new DecisionLog(LogCapacity);

        /// <summary>Turning the brain off also drops the stored plans, so nothing stale shows while cities run on the game's jobs.</summary>
        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (!value)
                {
                    ClearPlans();
                }
            }
        }

        /// <summary>Stores the plan and logs it when it differs from the city's previous plan.</summary>
        public static void RecordPlan(long cityId, string cityName, JobPlan plan)
        {
            JobPlan previous;
            lock (Gate)
            {
                LastPlans.TryGetValue(cityId, out previous);
                LastPlans[cityId] = plan;
                PruneDeadCities();
            }

            if (previous == null || previous.Summary != plan.Summary)
            {
                Decisions.Add(new Decision(DateTime.UtcNow, cityId, cityName, "plan", plan.Summary));
            }
        }

        /// <summary>Logs the error unless it repeats the city's last one. Returns true when it was new.</summary>
        public static bool RecordError(long cityId, string cityName, string error)
        {
            lock (Gate)
            {
                if (LastErrors.TryGetValue(cityId, out string last) && last == error)
                {
                    return false;
                }

                LastErrors[cityId] = error;
            }

            Decisions.Add(new Decision(DateTime.UtcNow, cityId, cityName, "error", error));
            return true;
        }

        public static JobPlan LastPlanFor(long cityId)
        {
            lock (Gate)
            {
                return LastPlans.TryGetValue(cityId, out JobPlan plan) ? plan : null;
            }
        }

        private static void ClearPlans()
        {
            lock (Gate)
            {
                LastPlans.Clear();
            }
        }

        // Call with Gate held. Cheap check first: the maps only outgrow the city list once a city is gone.
        private static void PruneDeadCities()
        {
            int liveCount = World.world.cities.Count;
            if (LastPlans.Count <= liveCount && LastErrors.Count <= liveCount)
            {
                return;
            }

            var live = new HashSet<long>(World.world.cities.Select(city => city.getID()));
            RemoveDead(LastPlans, live);
            RemoveDead(LastErrors, live);
        }

        private static void RemoveDead<TValue>(Dictionary<long, TValue> map, HashSet<long> live)
        {
            foreach (long id in map.Keys.Where(id => !live.Contains(id)).ToList())
            {
                map.Remove(id);
            }
        }
    }
}
