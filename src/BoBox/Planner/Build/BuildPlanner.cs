using System;
using System.Collections.Generic;
using BoBox.Planner.Capacity;
using BoBox.Planner.Scenario;

namespace BoBox.Planner.Build
{
    /// <summary>
    /// Picks one build order from candidates that already passed the game's checks: drop types at
    /// their maximum, prefer types below their minimum by pathway priority, otherwise pick by weight.
    /// Pure and deterministic for a given seed. Never returns null.
    /// </summary>
    /// <remarks>
    /// The weight of a candidate is 1 plus its pathway priority (a negative priority counts as 0).
    /// A type the pathway does not list has priority 0, so it keeps weight 1 and stays buildable;
    /// a listed priority only makes a type more likely, never less likely than an unlisted one.
    /// The seed is scrambled before it feeds <see cref="Random"/>, so consecutive seeds do not give
    /// correlated first draws.
    /// </remarks>
    /// <remarks>
    /// An upgrade replaces a building instead of adding one, so it never counts toward a type's
    /// maximum (it stays eligible when the type is full) and never counts toward its minimum (it
    /// cannot lift a type that is below its minimum, so it only competes in the weighted step).
    /// </remarks>
    public static class BuildPlanner
    {
        public static BuildChoice Choose(BuildSnapshot snapshot, Limits limits, Pathway pathway, int seed)
        {
            if (snapshot == null || snapshot.Candidates.Count == 0)
            {
                return BuildChoice.None("no candidates");
            }

            Limits effectiveLimits = limits ?? Limits.Unbounded;
            Pathway effectivePathway = pathway ?? Pathway.Neutral;
            IReadOnlyList<BuildCandidate> all = snapshot.Candidates;

            List<BuildCandidate> open = DropAtMax(all, effectiveLimits);
            if (open.Count == 0)
            {
                return BuildChoice.None("step 1 max: all " + all.Count + " candidates are at their building maximum");
            }

            var random = new Random(MixSeed(seed));
            string dropped = DroppedNote(all.Count, open.Count);

            List<BuildCandidate> belowMin = BelowMin(open, effectiveLimits);
            if (belowMin.Count > 0)
            {
                return ChooseBelowMin(belowMin, effectiveLimits, effectivePathway, random, dropped);
            }

            return ChooseWeighted(open, effectivePathway, random, dropped);
        }

        // System.Random's first draw is close to linear in a small seed, so sequential seeds would
        // pick in a visible pattern. This is the murmur3 32-bit finalizer, which spreads every input bit.
        private static int MixSeed(int seed)
        {
            unchecked
            {
                uint x = (uint)seed;
                x ^= x >> 16;
                x *= 0x85EBCA6Bu;
                x ^= x >> 13;
                x *= 0xC2B2AE35u;
                x ^= x >> 16;
                return (int)x;
            }
        }

        private static List<BuildCandidate> DropAtMax(IReadOnlyList<BuildCandidate> candidates, Limits limits)
        {
            var open = new List<BuildCandidate>(candidates.Count);
            foreach (BuildCandidate candidate in candidates)
            {
                bool full = !candidate.IsUpgrade
                    && limits.ForBuilding(candidate.BuildingType).IsAtMax(candidate.CurrentCount);
                if (!full)
                {
                    open.Add(candidate);
                }
            }

            return open;
        }

        private static List<BuildCandidate> BelowMin(List<BuildCandidate> open, Limits limits)
        {
            var below = new List<BuildCandidate>();
            foreach (BuildCandidate candidate in open)
            {
                if (!candidate.IsUpgrade && limits.ForBuilding(candidate.BuildingType).IsBelowMin(candidate.CurrentCount))
                {
                    below.Add(candidate);
                }
            }

            return below;
        }

        private static BuildChoice ChooseBelowMin(
            List<BuildCandidate> belowMin, Limits limits, Pathway pathway, Random random, string dropped)
        {
            int best = int.MinValue;
            var top = new List<BuildCandidate>();
            foreach (BuildCandidate candidate in belowMin)
            {
                int priority = pathway.PriorityForBuilding(candidate.BuildingType);
                if (priority > best)
                {
                    best = priority;
                    top.Clear();
                }

                if (priority == best)
                {
                    top.Add(candidate);
                }
            }

            BuildCandidate picked = top[top.Count == 1 ? 0 : random.Next(top.Count)];
            CountRange range = limits.ForBuilding(picked.BuildingType);
            string tie = top.Count == 1 ? "only one at top priority" : "seeded pick among " + top.Count + " tied";
            return new BuildChoice(
                picked,
                "step 2 min: " + belowMin.Count + " of the candidates are below their minimum; " + picked.BuildingType
                + " has " + picked.CurrentCount + " (range " + range + "), priority " + best + " on pathway '"
                + pathway.Id + "' (" + tie + ")" + dropped);
        }

        private static BuildChoice ChooseWeighted(List<BuildCandidate> open, Pathway pathway, Random random, string dropped)
        {
            var weights = new double[open.Count];
            double total = 0;
            for (int i = 0; i < open.Count; i++)
            {
                weights[i] = 1 + Math.Max(0, pathway.PriorityForBuilding(open[i].BuildingType));
                total += weights[i];
            }

            int index = PickIndex(weights, total, random.NextDouble() * total);
            BuildCandidate picked = open[index];
            return new BuildChoice(
                picked,
                "step 3 weighted: weight " + weights[index] + " of total " + total + " over " + open.Count
                + " candidates on pathway '" + pathway.Id + "'" + dropped);
        }

        private static int PickIndex(double[] weights, double total, double roll)
        {
            double running = 0;
            int last = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                running += weights[i];
                last = i;
                if (roll < running)
                {
                    return i;
                }
            }

            return last;
        }

        private static string DroppedNote(int before, int after)
        {
            return before == after ? string.Empty : "; step 1 dropped " + (before - after) + " at maximum";
        }
    }
}
