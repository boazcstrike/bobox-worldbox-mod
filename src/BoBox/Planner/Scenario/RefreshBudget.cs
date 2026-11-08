using System;
using BoBox.Rules;

namespace BoBox.Planner.Scenario
{
    /// <summary>
    /// How many cities the scenario cache may refresh in one tick. A fixed per-frame count falls behind
    /// once there are many cities, so the budget grows to cover every city once per refresh interval:
    /// max(cities_per_frame, ceil(cities x elapsed world seconds / refresh_seconds)), capped at
    /// <see cref="SignalBounds.MaxCitiesPerFrame"/>. Elapsed time counts at most <see cref="MaxElapsedSeconds"/>,
    /// so one long frame (a load, a stall) does not spend the cap. Pure.
    /// </summary>
    public static class RefreshBudget
    {
        /// <summary>The most world seconds of one tick that the budget accounts for.</summary>
        public const double MaxElapsedSeconds = 0.5;

        public static int For(int cityCount, double elapsedSeconds, SignalRules rules)
        {
            int floor = Math.Min(SignalBounds.MaxCitiesPerFrame, Math.Max(SignalBounds.MinCitiesPerFrame, rules.CitiesPerFrame));
            if (cityCount <= 0 || !(elapsedSeconds > 0) || !(rules.RefreshSeconds > 0))
            {
                return floor;
            }

            double needed = Math.Ceiling(cityCount * Math.Min(elapsedSeconds, MaxElapsedSeconds) / rules.RefreshSeconds);
            return (int)Math.Min(SignalBounds.MaxCitiesPerFrame, Math.Max(floor, needed));
        }
    }
}
