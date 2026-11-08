using System.Collections.Generic;
using BoBox.Planner;
using BoBox.Planner.Build;
using BoBox.Planner.Capacity;
using BoBox.Planner.Scenario;
using BoBox.Rules;
using Xunit;

namespace BoBox.Tests
{
    /// <summary>Default capacity tuning end to end: employment of a large city, the builder floor, and the house minimum.</summary>
    public class CapacityTuningTests
    {
        private const int Seeds = 200;

        private static Limits Default(AreaInfo area) => CapacityModel.Limits(area, Pathway.Neutral, ColonyRules.Default);

        // Every game gate open and far above any capacity limit, so only the limits decide.
        private static CitySnapshot AmpleCity(int adults, int pendingBuildings = 1) =>
            new CitySnapshot(
                adults: adults, food: 100000, wood: 100000, stone: 100000,
                hasStorage: true, hasWindmill: true, hasMine: true, pendingBuildings: pendingBuildings,
                bushes: 1000, plants: 1000, hives: 1000, trees: 1000, ores: 1000, cropTiles: 1000);

        [Fact]
        public void TwoHundredZoneCityEmploysAtLeastNinetyPercentOnTheNeutralPathway()
        {
            Limits limits = Default(new AreaInfo(zones: 200, landTiles: 200 * 64, adults: 300, population: 300));

            JobPlan plan = ColonyPlanner.Plan(AmpleCity(300), Pathway.Neutral, limits);

            // Ceilings at 300 people: farmer 120, bushes 30, herbs 15, honey 6, woodcutter 90, deposit 15, plus
            // hunter 1, mine 5 and builders 6.
            Assert.Equal(288, plan.TotalSlots);
            Assert.True(plan.TotalSlots >= 270, plan.Summary);
            Assert.Equal(12, plan.Idle);
            Assert.StartsWith("capped, idle 12", plan.IdleReason);
            Assert.EndsWith("idle=12", plan.Summary);
        }

        [Fact]
        public void FullyEmployedPlanHasNoIdleReason()
        {
            JobPlan plan = ColonyPlanner.Plan(AmpleCity(30), Pathway.Neutral, Default(new AreaInfo(zones: 20, adults: 30, population: 30)));

            Assert.Equal(30, plan.TotalSlots);
            Assert.Equal(0, plan.Idle);
            Assert.Equal(string.Empty, plan.IdleReason);
        }

        [Fact]
        public void JobCeilingGrowsWithPopulation()
        {
            int small = Default(new AreaInfo(zones: 200, adults: 100, population: 100)).ForJob(JobIds.Woodcutter).Max;
            int large = Default(new AreaInfo(zones: 200, adults: 300, population: 300)).ForJob(JobIds.Woodcutter).Max;

            Assert.Equal(30, small);
            Assert.Equal(90, large);
        }

        [Fact]
        public void PopulationCeilingNeverPassesTheAreaMax()
        {
            Limits limits = Default(new AreaInfo(zones: 20, adults: 300, population: 300));

            Assert.Equal(20, limits.ForJob(JobIds.Woodcutter).Max);
        }

        [Theory]
        [InlineData(0.0, 6)]
        [InlineData(1.0, 0)]
        public void PendingBuildingAlwaysGetsOneBuilder(double builderWeight, int builderMax)
        {
            var pathway = new Pathway("test", jobWeights: new Dictionary<string, double> { [JobIds.Builder] = builderWeight });
            var limits = new Limits(new Dictionary<string, CountRange> { [JobIds.Builder] = new CountRange(0, builderMax) });

            JobPlan plan = ColonyPlanner.Plan(AmpleCity(12), pathway, limits);

            Assert.Equal(ColonyPlanner.MinBuildersWhilePending, plan.CountFor(JobIds.Builder));
        }

        [Fact]
        public void BuilderMinimumDoesNotRaiseBuildersInTheNeedPhase()
        {
            var limits = new Limits(new Dictionary<string, CountRange> { [JobIds.Builder] = new CountRange(5, 10) });

            JobPlan plan = ColonyPlanner.Plan(AmpleCity(12), Pathway.Neutral, limits);

            Assert.Equal(4, plan.CountFor(JobIds.Builder));
        }

        [Fact]
        public void NoBuilderWithoutAPendingBuilding()
        {
            Assert.Equal(0, ColonyPlanner.Plan(AmpleCity(12, pendingBuildings: 0), Pathway.Neutral, null).CountFor(JobIds.Builder));
        }

        // 100 people in 15 houses fit when the houses are upgraded (house_4 holds 6, house_5 holds 7). The old
        // minimum (0.2 per person = 20 houses) kept forcing houses here; 0.1 (10 houses) lets other types compete.
        [Fact]
        public void UpgradedHousesNoLongerForceHouseBuilds()
        {
            Limits limits = Default(new AreaInfo(zones: 50, adults: 70, population: 100));
            var candidates = new BuildSnapshot(new[]
            {
                new BuildCandidate("house", BuildingTypes.House, currentCount: 15),
                new BuildCandidate("windmill", BuildingTypes.Windmill, currentCount: 2),
                new BuildCandidate("house_upgrade", BuildingTypes.House, isUpgrade: true, currentCount: 15),
            });

            int otherPicks = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                BuildChoice choice = BuildPlanner.Choose(candidates, limits, ColonyRules.Default.DefaultPathway, seed);
                if (choice.Candidate.OrderId != "house")
                {
                    otherPicks++;
                }
            }

            Assert.Equal(10, limits.ForBuilding(BuildingTypes.House).Min);
            Assert.True(otherPicks > 0, "windmill and the house upgrade never picked");
        }
    }
}
