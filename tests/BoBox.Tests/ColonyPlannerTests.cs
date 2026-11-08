using System;
using System.Collections.Generic;
using System.Linq;
using BoBox.Planner;
using BoBox.Planner.Capacity;
using BoBox.Planner.Scenario;
using Xunit;

namespace BoBox.Tests
{
    public class ColonyPlannerTests
    {
        private const int Seed = 20261108;

        private static readonly string[] FoodJobs =
        {
            JobIds.Farmer, JobIds.GathererBushes, JobIds.GathererHerbs, JobIds.GathererHoney, JobIds.Hunter,
        };

        private static CitySnapshot City(
            int adults = 12,
            int food = 500,
            int wood = 500,
            int stone = 500,
            bool hasWindmill = true,
            bool hasMine = false,
            int pendingBuildings = 0,
            IReadOnlyDictionary<string, int> baseSlots = null,
            bool hasStorage = true,
            int bushes = 20,
            int plants = 20,
            int hives = 5,
            int trees = 50)
        {
            return new CitySnapshot(
                adults: adults,
                food: food,
                wood: wood,
                stone: stone,
                hasStorage: hasStorage,
                hasWindmill: hasWindmill,
                hasMine: hasMine,
                pendingBuildings: pendingBuildings,
                bushes: bushes,
                plants: plants,
                hives: hives,
                trees: trees,
                ores: 10,
                cropTiles: 10,
                baseSlots: baseSlots);
        }

        private static int FoodSlots(JobPlan plan)
        {
            return FoodJobs.Sum(plan.CountFor);
        }

        [Fact]
        public void StarvingCityPutsMostAdultsOnFood()
        {
            JobPlan plan = ColonyPlanner.Plan(City(adults: 10, food: 0));

            Assert.True(FoodSlots(plan) > plan.TotalSlots - FoodSlots(plan));
            Assert.True(FoodSlots(plan) >= 7);
        }

        [Fact]
        public void FedCityWithLowWoodPutsMostOnWoodcutting()
        {
            JobPlan plan = ColonyPlanner.Plan(City(adults: 12, food: 500, wood: 0));

            int woodcutters = plan.CountFor(JobIds.Woodcutter);
            Assert.True(woodcutters > FoodSlots(plan));
            Assert.True(woodcutters > ColonyPlanner.Plan(City(adults: 12, food: 500, wood: 500)).CountFor(JobIds.Woodcutter));
        }

        [Fact]
        public void PendingBuildingGetsAtLeastOneBuilder()
        {
            Assert.True(ColonyPlanner.Plan(City(adults: 2, food: 0, pendingBuildings: 1)).CountFor(JobIds.Builder) >= 1);
            Assert.Equal(4, ColonyPlanner.Plan(City(adults: 12, pendingBuildings: 1)).CountFor(JobIds.Builder));
            Assert.Equal(0, ColonyPlanner.Plan(City(adults: 12, pendingBuildings: 0)).CountFor(JobIds.Builder));
        }

        [Fact]
        public void NoWindmillMeansNoFarmers()
        {
            JobPlan plan = ColonyPlanner.Plan(City(adults: 20, food: 0, hasWindmill: false));

            Assert.Equal(0, plan.CountFor(JobIds.Farmer));
            Assert.True(FoodSlots(plan) > 0);
        }

        [Theory]
        [InlineData(1, 0, 0)]
        [InlineData(3, 0, 500)]
        [InlineData(7, 500, 0)]
        [InlineData(15, 500, 500)]
        [InlineData(40, 10, 10)]
        [InlineData(100, 0, 0)]
        public void NeverMoreSlotsThanAdults(int adults, int food, int wood)
        {
            var baseSlots = new Dictionary<string, int>
            {
                [JobIds.Attacker] = adults,
                [JobIds.RoadBuilder] = 1,
                [JobIds.Cleaner] = 1,
            };
            JobPlan plan = ColonyPlanner.Plan(City(adults, food, wood, hasMine: true, pendingBuildings: 1, baseSlots: baseSlots));

            Assert.True(plan.TotalSlots <= adults);
            Assert.All(plan.Lines, line => Assert.True(line.Count > 0));
        }

        [Fact]
        public void ZeroAdultsGivesEmptyPlan()
        {
            JobPlan plan = ColonyPlanner.Plan(City(adults: 0, food: 0, pendingBuildings: 1));

            Assert.Empty(plan.Lines);
            Assert.Equal(0, plan.TotalSlots);
        }

        [Fact]
        public void MinersOnlyWhenFoodAndWoodAreOk()
        {
            Assert.Equal(0, ColonyPlanner.Plan(City(adults: 20, food: 0, hasMine: true)).CountFor(JobIds.Miner));
            Assert.Equal(0, ColonyPlanner.Plan(City(adults: 20, wood: 0, hasMine: true)).CountFor(JobIds.Miner));
            Assert.True(ColonyPlanner.Plan(City(adults: 20, stone: 0, hasMine: true)).CountFor(JobIds.Miner) > 0);
        }

        [Fact]
        public void StarvingCityWithNoFoodSourceStillMines()
        {
            JobPlan plan = ColonyPlanner.Plan(
                City(adults: 20, food: 0, hasWindmill: false, hasMine: true, bushes: 0, plants: 0, hives: 0));

            Assert.Equal(0, plan.CountFor(JobIds.Farmer));
            Assert.True(plan.CountFor(JobIds.Miner) > 0);
        }

        [Fact]
        public void LowWoodWithNoWoodSourceStillMines()
        {
            JobPlan plan = ColonyPlanner.Plan(City(adults: 20, wood: 0, hasMine: true, hasStorage: false, trees: 0));

            Assert.Equal(0, plan.CountFor(JobIds.Woodcutter));
            Assert.True(plan.CountFor(JobIds.Miner) > 0);
        }

        [Fact]
        public void StarvingCityWithAFoodSourceStillWaitsBeforeMining()
        {
            JobPlan plan = ColonyPlanner.Plan(City(adults: 20, food: 0, hasWindmill: false, hasMine: true, plants: 0, hives: 0));

            Assert.True(plan.CountFor(JobIds.GathererBushes) > 0);
            Assert.Equal(0, plan.CountFor(JobIds.Miner));
        }

        [Fact]
        public void EveryLineCarriesAReason()
        {
            JobPlan plan = ColonyPlanner.Plan(City(adults: 15, food: 10, wood: 0, pendingBuildings: 1));

            Assert.NotEmpty(plan.Lines);
            Assert.All(plan.Lines, line => Assert.False(string.IsNullOrWhiteSpace(line.Reason)));
        }

        [Fact]
        public void NullPathwayAndLimitsPlanLikeNeutralAndUnbounded()
        {
            var random = new Random(Seed);
            for (int i = 0; i < 300; i++)
            {
                CitySnapshot city = RandomCity(random);

                string expected = ColonyPlanner.Plan(city).Summary;

                Assert.Equal(expected, ColonyPlanner.Plan(city, null, null).Summary);
                Assert.Equal(expected, ColonyPlanner.Plan(city, Pathway.Neutral, Limits.Unbounded).Summary);
            }
        }

        [Fact]
        public void WoodcutterWeightTwoRaisesWoodcutters()
        {
            CitySnapshot city = City(adults: 12, food: 500, wood: 500);

            int neutral = ColonyPlanner.Plan(city).CountFor(JobIds.Woodcutter);
            int weighted = ColonyPlanner.Plan(city, Weighted(JobIds.Woodcutter, 2.0), null).CountFor(JobIds.Woodcutter);

            Assert.True(weighted > neutral, neutral + " -> " + weighted);
        }

        [Fact]
        public void ZeroWeightClosesAJob()
        {
            JobPlan plan = ColonyPlanner.Plan(City(adults: 20, food: 0), Weighted(JobIds.Farmer, 0), null);

            Assert.Equal(0, plan.CountFor(JobIds.Farmer));
            Assert.True(FoodSlots(plan) > 0);
        }

        [Fact]
        public void MaxClampRedistributesCutSlots()
        {
            CitySnapshot city = City(adults: 20, food: 500, wood: 0);
            Limits limits = JobLimits(JobIds.Woodcutter, new CountRange(0, 2));

            JobPlan free = ColonyPlanner.Plan(city);
            JobPlan capped = ColonyPlanner.Plan(city, null, limits);

            Assert.True(free.CountFor(JobIds.Woodcutter) > 2);
            Assert.Equal(2, capped.CountFor(JobIds.Woodcutter));
            Assert.Equal(free.TotalSlots, capped.TotalSlots);
            Assert.True(FoodSlots(capped) > FoodSlots(free));
        }

        [Fact]
        public void MinRaisesAJobWhileAdultsRemain()
        {
            CitySnapshot city = City(adults: 10, food: 500, wood: 500);
            Limits limits = JobLimits(JobIds.GathererHoney, new CountRange(3, 10));

            JobPlan free = ColonyPlanner.Plan(city);
            JobPlan raised = ColonyPlanner.Plan(city, null, limits);

            Assert.True(free.CountFor(JobIds.GathererHoney) < 3);
            Assert.Equal(3, raised.CountFor(JobIds.GathererHoney));
            Assert.Equal(free.TotalSlots, raised.TotalSlots);
        }

        [Fact]
        public void MinNeverOpensAJobTheGameGates()
        {
            JobPlan plan = ColonyPlanner.Plan(
                City(adults: 20, hasWindmill: false), null, JobLimits(JobIds.Farmer, new CountRange(5, 10)));

            Assert.Equal(0, plan.CountFor(JobIds.Farmer));
        }

        [Fact]
        public void TotalNeverAboveAdultsAndMaxAlwaysHoldsOverRandomCities()
        {
            var random = new Random(Seed);
            string[] jobs = FoodJobs
                .Concat(new[] { JobIds.Woodcutter, JobIds.Miner, JobIds.MinerDeposit, JobIds.Builder, JobIds.Attacker })
                .ToArray();
            for (int i = 0; i < 1000; i++)
            {
                CitySnapshot city = RandomCity(random);
                var ranges = jobs.ToDictionary(id => id, id => RandomRange(random));
                var weights = jobs.ToDictionary(id => id, id => random.NextDouble() * 3 - 0.3);

                JobPlan plan = ColonyPlanner.Plan(city, new Pathway("random", jobWeights: weights), new Limits(ranges));

                Assert.True(plan.TotalSlots <= city.Adults, plan.Summary + " adults " + city.Adults);
                Assert.All(plan.Lines, line => Assert.True(line.Count > 0));
                Assert.All(plan.Lines, line => Assert.True(line.Count <= Math.Max(line.JobId == JobIds.Builder ? ColonyPlanner.MinBuildersWhilePending : 0,
                    ranges.GetValueOrDefault(line.JobId, CountRange.Unbounded).Max), line.JobId));
            }
        }

        private static Pathway Weighted(string jobId, double weight) =>
            new Pathway("test", jobWeights: new Dictionary<string, double> { [jobId] = weight });

        private static Limits JobLimits(string jobId, CountRange range) =>
            new Limits(new Dictionary<string, CountRange> { [jobId] = range });

        private static CountRange RandomRange(Random random)
        {
            int min = random.Next(0, 8);
            return random.Next(0, 4) == 0 ? CountRange.Unbounded : new CountRange(min, min + random.Next(-2, 10));
        }

        private static CitySnapshot RandomCity(Random random)
        {
            int adults = random.Next(0, 120);
            return new CitySnapshot(
                adults: adults,
                food: random.Next(0, 800),
                wood: random.Next(0, 100),
                stone: random.Next(0, 80),
                hasStorage: random.Next(0, 5) > 0,
                hasWindmill: random.Next(0, 2) == 0,
                hasMine: random.Next(0, 2) == 0,
                pendingBuildings: random.Next(0, 2),
                bushes: random.Next(0, 30),
                plants: random.Next(0, 30),
                hives: random.Next(0, 6),
                trees: random.Next(0, 60),
                ores: random.Next(0, 15),
                cropTiles: random.Next(0, 20),
                baseSlots: new Dictionary<string, int>
                {
                    [JobIds.Attacker] = random.Next(0, adults + 1),
                    [JobIds.RoadBuilder] = random.Next(0, 2),
                });
        }
    }
}
