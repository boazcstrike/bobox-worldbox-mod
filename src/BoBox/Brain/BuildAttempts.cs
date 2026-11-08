using System;
using System.Collections.Generic;
using System.Linq;
using ai.behaviours;
using BoBox.Game;
using BoBox.Planner.Build;
using BoBox.Planner.Capacity;
using BoBox.Planner.Scenario;

namespace BoBox.Brain
{
    /// <summary>What <see cref="BuildAttempts.Run"/> did: the choice that was built (or None with the reason) and the picks that failed.</summary>
    internal sealed class BuildAttemptResult
    {
        public BuildChoice Choice { get; }
        public IReadOnlyList<BuildCandidate> Failed { get; }

        public BuildAttemptResult(BuildChoice choice, IReadOnlyList<BuildCandidate> failed)
        {
            Choice = choice;
            Failed = failed;
        }
    }

    /// <summary>
    /// The choose-then-build loop of one build tick. A candidate whose build fails is dropped from a new list and
    /// the planner chooses again, at most MaxBuildAttempts times, so one unbuildable pick (no free tile)
    /// cannot block the rest of the tick.
    /// </summary>
    internal static class BuildAttempts
    {
        // Failed builds (no free tile, a vanished upgrade target) drop that candidate and choose again, this many times a tick.
        private const int MaxBuildAttempts = 3;

        // An upgrade tries this many of the type's buildings (in random order) before it counts as failed.
        private const int MaxUpgradeTargets = 5;

        /// <summary>
        /// Failures are remembered in <paramref name="failures"/> in a finally, so a build that throws still
        /// puts its candidate, and the ones before it, on cooldown before the exception reaches the caller.
        /// <paramref name="onActed"/> runs the moment a building was placed or an upgrade started, before anything
        /// else can throw, so the caller still knows the city acted when a later call fails.
        /// </summary>
        public static BuildAttemptResult Run(
            City city,
            IReadOnlyList<KeyValuePair<BuildCandidate, BuildOrder>> found,
            CityScenario scenario,
            int seed,
            BuildFailureMemory failures,
            long cityId,
            double now,
            Action onActed)
        {
            var failed = new List<BuildCandidate>();
            try
            {
                BuildChoice choice = ChooseAndBuild(city, found, scenario, seed, failed, onActed);
                return new BuildAttemptResult(choice, failed);
            }
            finally
            {
                failures.Remember(cityId, failed, now);
            }
        }

        // failed is Run's own local list, the only thing this mutates.
        private static BuildChoice ChooseAndBuild(
            City city,
            IReadOnlyList<KeyValuePair<BuildCandidate, BuildOrder>> found,
            CityScenario scenario,
            int seed,
            List<BuildCandidate> failed,
            Action onActed)
        {
            IReadOnlyList<KeyValuePair<BuildCandidate, BuildOrder>> remaining = found;
            for (int attempt = 0; attempt < MaxBuildAttempts; attempt++)
            {
                BuildChoice choice = BuildPlanner.Choose(
                    new BuildSnapshot(remaining.Select(pair => pair.Key)),
                    scenario?.Limits ?? Limits.Unbounded,
                    scenario?.Pathway ?? Pathway.Neutral,
                    seed);
                if (!choice.HasCandidate)
                {
                    return failed.Count == 0 ? choice : BuildChoice.None(FailureReason(failed) + "; then " + choice.Reason);
                }

                // Lookup by reference: the planner hands back the instance it was given.
                KeyValuePair<BuildCandidate, BuildOrder> match = remaining.FirstOrDefault(
                    pair => ReferenceEquals(pair.Key, choice.Candidate));
                if (match.Key == null)
                {
                    return BuildChoice.None("the planner chose a candidate that was not offered: " + choice.Candidate);
                }

                if (TryBuild(city, match, failed, onActed))
                {
                    return choice;
                }

                failed.Add(choice.Candidate);
                string failedKey = BuildFailureMemory.KeyOf(choice.Candidate);
                remaining = remaining.Where(pair => BuildFailureMemory.KeyOf(pair.Key) != failedKey).ToList();
            }

            return BuildChoice.None(FailureReason(failed));
        }

        // A build that throws counts as failed for the cooldown, then the exception goes on to the caller.
        private static bool TryBuild(
            City city,
            KeyValuePair<BuildCandidate, BuildOrder> match,
            List<BuildCandidate> failed,
            Action onActed)
        {
            try
            {
                return Build(city, match.Value, onActed);
            }
            catch
            {
                failed.Add(match.Key);
                throw;
            }
        }

        private static string FailureReason(IReadOnlyList<BuildCandidate> failed)
        {
            return "build failed for " + string.Join(", ", failed);
        }

        // CityBehBuild.cs lines 99-123: upgrades go to a random building of the asset, new buildings through tryToBuild.
        // True only when a building was placed or an upgrade started (tryToBuild returns null, upgradeBuilding false, on failure).
        // An upgrade tries up to MaxUpgradeTargets of the type's buildings in random order (LoopRandom, as the game's
        // upgradeRandomBuilding does, CityBehBuild.cs lines 134-148) before it reports failure.
        // onActed runs right after the game call succeeded.
        private static bool Build(City city, BuildOrder order, Action onActed)
        {
            BuildingAsset asset = order.getBuildingAsset(city);
            if (!order.upgrade)
            {
                if (CityBehBuild.tryToBuild(city, asset) == null)
                {
                    return false;
                }

                onActed();
                return true;
            }

            IReadOnlyList<Building> existing = CityInternals.GetBuildingListOfId(city, asset.id);
            if (existing == null || existing.Count == 0)
            {
                return false;
            }

            // A copy: LoopRandom needs a List, and the city's own list must not be walked while an upgrade changes it.
            // A building that is dying or a ruin can still sit in the list (the city prunes it lazily), and
            // upgradeBuilding checks neither: Building.isUsable (Building.cs line 861) rules it out first.
            foreach (Building target in new List<Building>(existing).LoopRandom(MaxUpgradeTargets))
            {
                if (!target.isUsable())
                {
                    continue;
                }

                if (CityBehBuild.upgradeBuilding(target, city))
                {
                    onActed();
                    return true;
                }
            }

            return false;
        }
    }
}
