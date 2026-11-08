using System.Collections.Generic;
using BoBox.Planner.Build;
using BoBox.Planner.Capacity;
using BoBox.Planner.Scenario;
using Xunit;

namespace BoBox.Tests
{
    public class BuildPlannerTests
    {
        private static Limits LimitsFor(params (string type, int min, int max)[] entries)
        {
            var buildings = new Dictionary<string, CountRange>();
            foreach ((string type, int min, int max) in entries)
            {
                buildings[type] = new CountRange(min, max);
            }

            return new Limits(buildings: buildings);
        }

        private static Pathway PriorityPathway(params (string type, int priority)[] entries)
        {
            var priorities = new Dictionary<string, int>();
            foreach ((string type, int priority) in entries)
            {
                priorities[type] = priority;
            }

            return new Pathway("test", buildingPriority: priorities);
        }

        private static BuildSnapshot Snapshot(params BuildCandidate[] candidates) => new BuildSnapshot(candidates);

        [Fact]
        public void NoCandidatesGivesNoChoiceWithAReason()
        {
            BuildChoice choice = BuildPlanner.Choose(BuildSnapshot.Empty, Limits.Unbounded, Pathway.Neutral, seed: 1);

            Assert.False(choice.HasCandidate);
            Assert.NotEmpty(choice.Reason);
        }

        [Fact]
        public void AllCandidatesAtMaxGivesNoChoiceNamingTheStep()
        {
            BuildSnapshot snapshot = Snapshot(
                new BuildCandidate("well", BuildingTypes.Well, currentCount: 2),
                new BuildCandidate("mine", BuildingTypes.Mine, currentCount: 1));
            Limits limits = LimitsFor((BuildingTypes.Well, 0, 2), (BuildingTypes.Mine, 0, 1));

            BuildChoice choice = BuildPlanner.Choose(snapshot, limits, Pathway.Neutral, seed: 1);

            Assert.False(choice.HasCandidate);
            Assert.Contains("step 1", choice.Reason);
            Assert.Contains("2", choice.Reason);
        }

        [Fact]
        public void TypeAtMaxIsDroppedAndTheOtherIsPicked()
        {
            BuildCandidate full = new BuildCandidate("well", BuildingTypes.Well, currentCount: 3);
            BuildCandidate open = new BuildCandidate("mine", BuildingTypes.Mine, currentCount: 0);
            Limits limits = LimitsFor((BuildingTypes.Well, 0, 3));

            for (int seed = 0; seed < 20; seed++)
            {
                BuildChoice choice = BuildPlanner.Choose(Snapshot(full, open), limits, Pathway.Neutral, seed);

                Assert.Same(open, choice.Candidate);
                Assert.Contains("dropped 1", choice.Reason);
            }
        }

        [Fact]
        public void BelowMinBeatsAHigherWeightedType()
        {
            BuildCandidate needed = new BuildCandidate("well", BuildingTypes.Well, currentCount: 0);
            BuildCandidate popular = new BuildCandidate("house", BuildingTypes.House, currentCount: 5);
            Limits limits = LimitsFor((BuildingTypes.Well, 1, 4));
            Pathway pathway = PriorityPathway((BuildingTypes.Well, 1), (BuildingTypes.House, 100));

            for (int seed = 0; seed < 20; seed++)
            {
                BuildChoice choice = BuildPlanner.Choose(Snapshot(popular, needed), limits, pathway, seed);

                Assert.Same(needed, choice.Candidate);
                Assert.Contains("step 2", choice.Reason);
            }
        }

        [Fact]
        public void HighestPriorityWinsAmongTypesBelowMin()
        {
            BuildCandidate low = new BuildCandidate("well", BuildingTypes.Well);
            BuildCandidate high = new BuildCandidate("mine", BuildingTypes.Mine);
            BuildCandidate mid = new BuildCandidate("stock", BuildingTypes.Stockpile);
            Limits limits = LimitsFor(
                (BuildingTypes.Well, 1, 4), (BuildingTypes.Mine, 1, 4), (BuildingTypes.Stockpile, 1, 4));
            Pathway pathway = PriorityPathway(
                (BuildingTypes.Well, 1), (BuildingTypes.Mine, 9), (BuildingTypes.Stockpile, 5));

            for (int seed = 0; seed < 20; seed++)
            {
                Assert.Same(high, BuildPlanner.Choose(Snapshot(low, high, mid), limits, pathway, seed).Candidate);
            }
        }

        [Fact]
        public void TiedBelowMinPriorityIsSeededAndReachesBothTypes()
        {
            BuildCandidate first = new BuildCandidate("well", BuildingTypes.Well);
            BuildCandidate second = new BuildCandidate("mine", BuildingTypes.Mine);
            Limits limits = LimitsFor((BuildingTypes.Well, 1, 4), (BuildingTypes.Mine, 1, 4));
            var seen = new HashSet<string>();

            for (int seed = 0; seed < 50; seed++)
            {
                seen.Add(BuildPlanner.Choose(Snapshot(first, second), limits, Pathway.Neutral, seed).Candidate.OrderId);
            }

            Assert.Equal(2, seen.Count);
        }

        [Fact]
        public void WeightedPickFollowsPriorityRatio()
        {
            BuildCandidate light = new BuildCandidate("well", BuildingTypes.Well);
            BuildCandidate heavy = new BuildCandidate("house", BuildingTypes.House);
            Pathway pathway = PriorityPathway((BuildingTypes.Well, 1), (BuildingTypes.House, 5));
            int heavyPicks = 0;
            const int draws = 1000;

            for (int seed = 0; seed < draws; seed++)
            {
                if (BuildPlanner.Choose(Snapshot(light, heavy), Limits.Unbounded, pathway, seed).Candidate == heavy)
                {
                    heavyPicks++;
                }
            }

            double ratio = heavyPicks / (double)draws;
            Assert.InRange(ratio, 0.70, 0.80);
        }

        [Fact]
        public void NegativePriorityCountsAsUnlisted()
        {
            BuildCandidate unlisted = new BuildCandidate("well", BuildingTypes.Well);
            BuildCandidate negative = new BuildCandidate("mine", BuildingTypes.Mine);
            Pathway pathway = PriorityPathway((BuildingTypes.Mine, -5));
            int negativePicks = 0;
            const int draws = 1000;

            for (int seed = 0; seed < draws; seed++)
            {
                if (BuildPlanner.Choose(Snapshot(unlisted, negative), Limits.Unbounded, pathway, seed).Candidate == negative)
                {
                    negativePicks++;
                }
            }

            Assert.InRange(negativePicks / (double)draws, 0.42, 0.58);
        }

        [Fact]
        public void TypeMissingFromAPathwayThatListsOthersIsStillPicked()
        {
            BuildCandidate unlisted = new BuildCandidate("well", BuildingTypes.Well);
            BuildCandidate listed = new BuildCandidate("house", BuildingTypes.House);
            Pathway pathway = PriorityPathway((BuildingTypes.House, 5));
            int unlistedPicks = 0;
            const int draws = 2000;

            for (int seed = 0; seed < draws; seed++)
            {
                if (BuildPlanner.Choose(Snapshot(unlisted, listed), Limits.Unbounded, pathway, seed).Candidate == unlisted)
                {
                    unlistedPicks++;
                }
            }

            Assert.InRange(unlistedPicks / (double)draws, 0.10, 0.19);
        }

        [Fact]
        public void SequentialSeedsDoNotAlternateBetweenEqualCandidates()
        {
            BuildCandidate first = new BuildCandidate("well", BuildingTypes.Well);
            BuildCandidate second = new BuildCandidate("mine", BuildingTypes.Mine);
            const int firstSeed = 100000;
            const int seeds = 100;
            const int maxAlternatingRun = 20;
            int longest = 1;
            int run = 1;
            BuildCandidate previous = null;

            for (int seed = firstSeed; seed < firstSeed + seeds; seed++)
            {
                BuildCandidate picked = BuildPlanner.Choose(Snapshot(first, second), Limits.Unbounded, Pathway.Neutral, seed).Candidate;
                run = previous != null && picked != previous ? run + 1 : 1;
                longest = System.Math.Max(longest, run);
                previous = picked;
            }

            Assert.True(longest < maxAlternatingRun, "longest alternating run was " + longest);
        }

        [Fact]
        public void AllZeroWeightsPickUniformly()
        {
            BuildCandidate a = new BuildCandidate("well", BuildingTypes.Well);
            BuildCandidate b = new BuildCandidate("mine", BuildingTypes.Mine);
            BuildCandidate c = new BuildCandidate("house", BuildingTypes.House);
            var counts = new Dictionary<string, int> { ["well"] = 0, ["mine"] = 0, ["house"] = 0 };
            const int draws = 900;

            for (int seed = 0; seed < draws; seed++)
            {
                BuildChoice choice = BuildPlanner.Choose(Snapshot(a, b, c), Limits.Unbounded, Pathway.Neutral, seed);
                counts[choice.Candidate.OrderId]++;
                Assert.Contains("step 3", choice.Reason);
                Assert.Contains("weight 1 of total 3", choice.Reason);
            }

            foreach (int count in counts.Values)
            {
                Assert.InRange(count / (double)draws, 0.27, 0.40);
            }
        }

        [Fact]
        public void SameSeedGivesTheSamePickAndReason()
        {
            BuildSnapshot snapshot = Snapshot(
                new BuildCandidate("well", BuildingTypes.Well),
                new BuildCandidate("mine", BuildingTypes.Mine),
                new BuildCandidate("house", BuildingTypes.House));
            Pathway pathway = PriorityPathway((BuildingTypes.Well, 2), (BuildingTypes.Mine, 3), (BuildingTypes.House, 4));

            for (int seed = 0; seed < 20; seed++)
            {
                BuildChoice first = BuildPlanner.Choose(snapshot, Limits.Unbounded, pathway, seed);
                BuildChoice second = BuildPlanner.Choose(snapshot, Limits.Unbounded, pathway, seed);

                Assert.Same(first.Candidate, second.Candidate);
                Assert.Equal(first.Reason, second.Reason);
            }
        }

        [Fact]
        public void UpgradeAtMaxStaysEligible()
        {
            BuildCandidate upgrade = new BuildCandidate("house_up", BuildingTypes.House, isUpgrade: true, currentCount: 4);
            Limits limits = LimitsFor((BuildingTypes.House, 0, 4));

            BuildChoice choice = BuildPlanner.Choose(Snapshot(upgrade), limits, Pathway.Neutral, seed: 3);

            Assert.Same(upgrade, choice.Candidate);
        }

        [Fact]
        public void NewBuildingAtTheSameMaxIsDropped()
        {
            BuildCandidate fresh = new BuildCandidate("house_new", BuildingTypes.House, currentCount: 4);
            Limits limits = LimitsFor((BuildingTypes.House, 0, 4));

            Assert.False(BuildPlanner.Choose(Snapshot(fresh), limits, Pathway.Neutral, seed: 3).HasCandidate);
        }

        [Fact]
        public void UpgradeWithHigherPriorityLosesToABelowMinFreshCandidate()
        {
            BuildCandidate upgrade = new BuildCandidate("house_up", BuildingTypes.House, isUpgrade: true, currentCount: 1);
            BuildCandidate sameTypeUpgrade = new BuildCandidate("well_up", BuildingTypes.Well, isUpgrade: true, currentCount: 1);
            BuildCandidate fresh = new BuildCandidate("well_new", BuildingTypes.Well, currentCount: 0);
            Pathway pathway = PriorityPathway((BuildingTypes.House, 9), (BuildingTypes.Well, 1));
            Limits limits = LimitsFor((BuildingTypes.House, 1, 4), (BuildingTypes.Well, 1, 4));

            for (int seed = 0; seed < 20; seed++)
            {
                BuildChoice choice = BuildPlanner.Choose(Snapshot(upgrade, sameTypeUpgrade, fresh), limits, pathway, seed);

                Assert.Same(fresh, choice.Candidate);
                Assert.Contains("step 2", choice.Reason);
            }
        }

        [Fact]
        public void NullLimitsAndPathwayFallBackToNeutral()
        {
            BuildCandidate only = new BuildCandidate("well", BuildingTypes.Well);

            BuildChoice choice = BuildPlanner.Choose(Snapshot(only), null, null, seed: 1);

            Assert.Same(only, choice.Candidate);
            Assert.NotEmpty(choice.Reason);
        }
    }
}
