using System.Collections.Generic;
using BoBox.Planner;
using BoBox.Planner.Capacity;
using BoBox.Planner.Scenario;
using Xunit;

namespace BoBox.Tests
{
    /// <summary>Pathway weights split slots in proportion; kept jobs only take the maximum.</summary>
    public class ColonyPlannerWeightTests
    {
        private const double RatioTolerance = 0.25;

        // Only farmers and bush gatherers can take food slots, with room for every adult.
        private static CitySnapshot FarmAndBushCity(int adults, IReadOnlyDictionary<string, int> baseSlots = null)
        {
            return new CitySnapshot(
                adults: adults,
                food: 0,
                wood: 500,
                stone: 500,
                hasStorage: true,
                hasWindmill: true,
                bushes: 200,
                trees: 0,
                cropTiles: 200,
                baseSlots: baseSlots);
        }

        // Farmers take food and woodcutters take wood; nothing else is open, and food is below target.
        private static CitySnapshot FarmOnlyCity(int adults)
        {
            return new CitySnapshot(
                adults: adults, food: 0, wood: 500, stone: 500, hasStorage: true, hasWindmill: true,
                trees: 200, cropTiles: 200);
        }

        private static Pathway Weights(params (string JobId, double Weight)[] weights)
        {
            var map = new Dictionary<string, double> { [JobIds.Hunter] = 0 };
            foreach ((string jobId, double weight) in weights)
            {
                map[jobId] = weight;
            }

            return new Pathway("test", jobWeights: map);
        }

        private static double Ratio(JobPlan plan) =>
            (double)plan.CountFor(JobIds.Farmer) / plan.CountFor(JobIds.GathererBushes);

        [Theory]
        [InlineData(20)]
        [InlineData(40)]
        [InlineData(100)]
        public void FoodSlotsSplitInProportionToJobWeights(int adults)
        {
            JobPlan plan = ColonyPlanner.Plan(
                FarmAndBushCity(adults), Weights((JobIds.Farmer, 1.5), (JobIds.GathererBushes, 0.8)), null);

            Assert.InRange(Ratio(plan), 1.5 / 0.8 - RatioTolerance, 1.5 / 0.8 + RatioTolerance);
            Assert.True(plan.TotalSlots <= adults, plan.Summary);
        }

        [Fact]
        public void EqualWeightsSplitEvenly()
        {
            JobPlan plan = ColonyPlanner.Plan(FarmAndBushCity(40), Weights(), null);

            Assert.Equal(plan.CountFor(JobIds.Farmer), plan.CountFor(JobIds.GathererBushes));
        }

        [Fact]
        public void UnlistedJobWeighsNeutral()
        {
            JobPlan plan = ColonyPlanner.Plan(FarmAndBushCity(60), Weights((JobIds.Farmer, 2.0)), null);

            Assert.InRange(Ratio(plan), 2.0 - RatioTolerance, 2.0 + RatioTolerance);
        }

        // Food is fine and wood is low, so the surplus goes to woodcutters first; only the minimum keeps bushes ahead.
        // (While starving the surplus goes to food first, which would fill the bushes with or without a minimum.)
        private static CitySnapshot FarmBushAndWoodCity(int adults)
        {
            return new CitySnapshot(
                adults: adults, food: 1000, wood: 0, stone: 500, hasStorage: true, hasWindmill: true,
                bushes: 200, trees: 200, cropTiles: 200);
        }

        [Fact]
        public void ProportionalSplitStillHonoursMaxAndMin()
        {
            var weights = Weights((JobIds.Farmer, 1.5), (JobIds.GathererBushes, 0.8));
            var withMin = new Limits(new Dictionary<string, CountRange>
            {
                [JobIds.Farmer] = new CountRange(0, 5),
                [JobIds.GathererBushes] = new CountRange(8, 200),
            });
            var withoutMin = new Limits(new Dictionary<string, CountRange>
            {
                [JobIds.Farmer] = new CountRange(0, 5),
                [JobIds.GathererBushes] = new CountRange(0, 200),
            });

            JobPlan plan = ColonyPlanner.Plan(FarmBushAndWoodCity(40), weights, withMin);
            JobPlan baseline = ColonyPlanner.Plan(FarmBushAndWoodCity(40), weights, withoutMin);

            Assert.Equal(5, plan.CountFor(JobIds.Farmer));
            Assert.True(plan.CountFor(JobIds.GathererBushes) > baseline.CountFor(JobIds.GathererBushes), plan.Summary);
            Assert.True(plan.TotalSlots <= 40, plan.Summary);
        }

        // Starving food share 0.7 x group weight, capped at everyone; wood is the only other group.
        [Theory]
        [InlineData(0.5, 5)]
        [InlineData(1.0, 2)]
        [InlineData(1.2, 1)]
        public void GroupShareScalesByMeanWeight(double farmerWeight, int woodcutters)
        {
            JobPlan plan = ColonyPlanner.Plan(FarmOnlyCity(40), Weights((JobIds.Farmer, farmerWeight)), null);

            Assert.Equal(woodcutters, plan.CountFor(JobIds.Woodcutter));
        }

        [Fact]
        public void GroupShareSaturatesAtTheWholeShare()
        {
            JobPlan three = ColonyPlanner.Plan(FarmOnlyCity(40), Weights((JobIds.Farmer, 3.0)), null);
            JobPlan ten = ColonyPlanner.Plan(FarmOnlyCity(40), Weights((JobIds.Farmer, 10.0)), null);

            Assert.Equal(three.Summary, ten.Summary);
            Assert.Equal(0, ten.CountFor(JobIds.Woodcutter));
        }

        [Fact]
        public void SurplusSplitCountsSlotsAlreadyGiven()
        {
            var limits = new Limits(new Dictionary<string, CountRange>
            {
                [JobIds.GathererBushes] = new CountRange(20, 200),
            });
            var city = new CitySnapshot(
                adults: 40, food: 500, wood: 500, stone: 500, hasStorage: true, hasWindmill: true,
                bushes: 200, cropTiles: 200);

            JobPlan plan = ColonyPlanner.Plan(
                city, Weights((JobIds.Farmer, 2.0), (JobIds.GathererBushes, 1.0)), limits);

            Assert.True(plan.CountFor(JobIds.Farmer) >= plan.CountFor(JobIds.GathererBushes), plan.Summary);
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void NonFiniteWeightCountsAsNeutral(double weight)
        {
            CitySnapshot city = FarmAndBushCity(30);

            JobPlan neutral = ColonyPlanner.Plan(city, Weights(), null);
            JobPlan broken = ColonyPlanner.Plan(city, Weights((JobIds.Farmer, weight)), null);

            Assert.Equal(neutral.Summary, broken.Summary);
        }

        [Fact]
        public void NegativeInfiniteWeightClosesTheJob()
        {
            JobPlan plan = ColonyPlanner.Plan(
                FarmAndBushCity(30), Weights((JobIds.Farmer, double.NegativeInfinity)), null);

            Assert.Equal(0, plan.CountFor(JobIds.Farmer));
            Assert.Equal(30, plan.CountFor(JobIds.GathererBushes));
        }

        [Fact]
        public void KeptJobsAreNeverRaisedToTheirMinimum()
        {
            var limits = new Limits(new Dictionary<string, CountRange>
            {
                [JobIds.Attacker] = new CountRange(3, 10),
                [JobIds.RoadBuilder] = new CountRange(2, 10),
                [JobIds.Cleaner] = new CountRange(2, 10),
                [JobIds.ManureCleaner] = new CountRange(2, 10),
            });

            JobPlan plan = ColonyPlanner.Plan(FarmAndBushCity(30), null, limits);

            Assert.Equal(0, plan.CountFor(JobIds.Attacker));
            Assert.Equal(0, plan.CountFor(JobIds.RoadBuilder));
            Assert.Equal(0, plan.CountFor(JobIds.Cleaner));
            Assert.Equal(0, plan.CountFor(JobIds.ManureCleaner));
        }

        [Fact]
        public void KeptJobsAreLoweredToTheirMaximum()
        {
            var baseSlots = new Dictionary<string, int> { [JobIds.Attacker] = 8, [JobIds.RoadBuilder] = 1 };
            var limits = new Limits(new Dictionary<string, CountRange>
            {
                [JobIds.Attacker] = new CountRange(0, 3),
                [JobIds.RoadBuilder] = new CountRange(4, 10),
            });

            JobPlan plan = ColonyPlanner.Plan(FarmAndBushCity(30, baseSlots), null, limits);

            Assert.Equal(3, plan.CountFor(JobIds.Attacker));
            Assert.Equal(1, plan.CountFor(JobIds.RoadBuilder));
        }
    }
}
