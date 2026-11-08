using System;
using BoBox.Planner.Build;
using BoBox.Planner.Capacity;
using BoBox.Planner.Scenario;

namespace BoBox.Brain
{
    /// <summary>
    /// One city's cached scenario state, as <see cref="ScenarioCache"/> holds it. Immutable: an update
    /// replaces the whole entry. Signals and Result are null until the city's first refresh.
    /// </summary>
    internal sealed class CityScenario
    {
        public long CityId { get; }
        public string CityName { get; }
        public ScenarioSignals Signals { get; }

        /// <summary>The classification in use: the classifier's tags and scores, with the pathway <see cref="Hold"/> kept.</summary>
        public ScenarioResult Result { get; }

        public Limits Limits { get; }

        /// <summary>The build step's last pick and reason, or null when it has not chosen yet.</summary>
        public BuildChoice LastBuild { get; }

        /// <summary>
        /// World time (seconds, map_stats.world_time) of the last signals refresh. Meaningless while
        /// <see cref="IsRefreshed"/> is false.
        /// </summary>
        public double RefreshedAt { get; }

        /// <summary>World time (same clock as <see cref="RefreshedAt"/>) of <see cref="LastBuild"/>, or 0 when there is none.</summary>
        public double LastBuildAt { get; }

        /// <summary>
        /// <see cref="ActiveRules.LoadedAtUtc"/> of the rules the last refresh used, so a reader can tell
        /// whether the city already runs on reloaded rules. Null before the first refresh or the first load.
        /// </summary>
        public DateTime? RulesLoadedAtUtc { get; }

        /// <summary>Pathway hysteresis state; <see cref="PathwayHold.None"/> before the first refresh.</summary>
        public PathwayHold Hold { get; }

        public CityScenario(
            long cityId,
            string cityName,
            ScenarioSignals signals,
            ScenarioResult result,
            Limits limits,
            BuildChoice lastBuild,
            double refreshedAt,
            double lastBuildAt,
            DateTime? rulesLoadedAtUtc = null,
            PathwayHold hold = null)
        {
            CityId = cityId;
            CityName = cityName ?? string.Empty;
            Signals = signals;
            Result = result;
            Limits = limits ?? Limits.Unbounded;
            LastBuild = lastBuild;
            RefreshedAt = refreshedAt;
            LastBuildAt = lastBuildAt;
            RulesLoadedAtUtc = rulesLoadedAtUtc;
            Hold = hold ?? PathwayHold.None;
        }

        /// <summary>False for a bare entry made by a build choice before the city's first refresh.</summary>
        public bool IsRefreshed => Signals != null;

        /// <summary>The city's pathway, or <see cref="Pathway.Neutral"/> before the first refresh.</summary>
        public Pathway Pathway => Result?.Pathway ?? Pathway.Neutral;

        public CityScenario WithLastBuild(BuildChoice choice, double lastBuildAt) =>
            new CityScenario(CityId, CityName, Signals, Result, Limits, choice, RefreshedAt, lastBuildAt, RulesLoadedAtUtc, Hold);

        /// <summary>A refreshed copy under the city's current name, keeping the build step's last choice and its time.</summary>
        public CityScenario With(CityScenario refreshed) =>
            new CityScenario(
                CityId, refreshed.CityName, refreshed.Signals, refreshed.Result, refreshed.Limits, LastBuild,
                refreshed.RefreshedAt, LastBuildAt, refreshed.RulesLoadedAtUtc, refreshed.Hold);
    }
}
