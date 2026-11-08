using System;
using System.Collections.Generic;
using BoBox.Planner;
using BoBox.Planner.Scenario;
using Xunit;

namespace BoBox.Tests
{
    /// <summary>Edge cases from the planner hunt: mining from caps, the builder reserve, starving surplus, idle reasons.</summary>
    public class PlannerEdgeTests
    {
        private const int Seed = 20261108;

        private static Pathway Weights(params (string JobId, double Weight)[] weights)
        {
            var map = new Dictionary<string, double>();
            foreach ((string jobId, double weight) in weights)
            {
                map[jobId] = weight;
            }

            return new Pathway("test", jobWeights: map);
        }

        private static CitySnapshot Starving(int adults, int cropTiles = 0, int trees = 0, int ores = 0,
            int wood = 500, int pendingBuildings = 0, int food = 0) =>
            new CitySnapshot(
                adults: adults, food: food, wood: wood, stone: 0, hasStorage: true, hasWindmill: true,
                pendingBuildings: pendingBuildings, trees: trees, ores: ores, cropTiles: cropTiles);

        // J1: starving, but the only food jobs (farmer 2 slots, hunter 1) are full, so mining must not wait.
        [Fact]
        public void FullFoodJobsDoNotBlockMining()
        {
            JobPlan plan = ColonyPlanner.Plan(Starving(20, cropTiles: 2, ores: 10), null, null);

            Assert.Equal(2, plan.CountFor(JobIds.Farmer));
            Assert.Equal(1, plan.CountFor(JobIds.Hunter));
            Assert.Equal(10, plan.CountFor(JobIds.MinerDeposit));
            Assert.Equal(7, plan.Idle);
        }

        [Fact]
        public void ClosedFoodJobsDoNotBlockMining()
        {
            Pathway noFood = Weights(
                (JobIds.Farmer, 0), (JobIds.GathererBushes, 0), (JobIds.GathererHerbs, 0),
                (JobIds.GathererHoney, 0), (JobIds.Hunter, 0));

            JobPlan plan = ColonyPlanner.Plan(Starving(20, cropTiles: 20, ores: 10), noFood, null);

            Assert.Equal(10, plan.CountFor(JobIds.MinerDeposit));
            Assert.Equal(10, plan.Idle);
        }

        [Fact]
        public void ClosedWoodcutterDoesNotBlockMiningWhenWoodIsLow()
        {
            CitySnapshot city = Starving(20, trees: 20, ores: 10, wood: 0, food: 1000);

            JobPlan plan = ColonyPlanner.Plan(city, Weights((JobIds.Woodcutter, 0)), null);

            Assert.Equal(0, plan.CountFor(JobIds.Woodcutter));
            Assert.Equal(10, plan.CountFor(JobIds.MinerDeposit));
        }

        [Fact]
        public void OpenFoodJobsStillBlockMiningWhileStarving()
        {
            JobPlan plan = ColonyPlanner.Plan(Starving(20, cropTiles: 20, ores: 10), null, null);

            Assert.Equal(0, plan.CountFor(JobIds.MinerDeposit));
            Assert.Equal(0, plan.Idle);
        }

        // J2: the builder floor is reserved before food.
        [Fact]
        public void LoneStarvingAdultBuildsWhenABuildingWaits()
        {
            JobPlan plan = ColonyPlanner.Plan(Starving(1, cropTiles: 20, pendingBuildings: 1), null, null);

            Assert.Equal(1, plan.CountFor(JobIds.Builder));
            Assert.Equal(1, plan.TotalSlots);
        }

        [Fact]
        public void StarvingColonyWithWeightedFarmersStillKeepsABuilder()
        {
            JobPlan plan = ColonyPlanner.Plan(
                Starving(10, cropTiles: 20, pendingBuildings: 1), Weights((JobIds.Farmer, 2)), null);

            Assert.True(plan.CountFor(JobIds.Builder) >= 1, plan.Summary);
            Assert.True(plan.TotalSlots <= 10, plan.Summary);
        }

        [Fact]
        public void WarriorsTakingEveryAdultLeaveNoBuilder()
        {
            var baseSlots = new Dictionary<string, int> { [JobIds.Attacker] = 5 };
            var city = new CitySnapshot(adults: 5, food: 0, hasStorage: true, hasWindmill: true,
                pendingBuildings: 1, cropTiles: 20, baseSlots: baseSlots);

            JobPlan plan = ColonyPlanner.Plan(city, null, null);

            Assert.Equal(5, plan.CountFor(JobIds.Attacker));
            Assert.Equal(0, plan.CountFor(JobIds.Builder));
        }

        [Fact]
        public void BuilderIsReservedAfterWarriorsBeforeFood()
        {
            JobPlan plan = ColonyPlanner.Plan(Starving(10, cropTiles: 20, pendingBuildings: 1), null, null);

            Assert.Equal(JobIds.Builder, plan.Lines[0].JobId);
        }

        // J3: surplus fills food jobs with room before woodcutters while starving.
        [Fact]
        public void StarvingSurplusFillsFoodBeforeWood()
        {
            JobPlan plan = ColonyPlanner.Plan(Starving(20, cropTiles: 20, trees: 20, wood: 0), null, null);

            int food = plan.CountFor(JobIds.Farmer) + plan.CountFor(JobIds.Hunter);
            Assert.Equal(20, plan.TotalSlots);
            Assert.Equal(17, food);
            Assert.Equal(3, plan.CountFor(JobIds.Woodcutter));
        }

        // J4: the idle reason says what is really closed.
        [Fact]
        public void IdleReasonNamesClosedJobs()
        {
            Pathway closed = Weights((JobIds.Farmer, 0), (JobIds.Woodcutter, 0), (JobIds.MinerDeposit, 0),
                (JobIds.Miner, 0), (JobIds.Hunter, 0), (JobIds.GathererBushes, 0), (JobIds.GathererHerbs, 0),
                (JobIds.GathererHoney, 0));

            JobPlan plan = ColonyPlanner.Plan(Starving(8, cropTiles: 20, trees: 20, ores: 10), closed, null);

            Assert.Equal(8, plan.Idle);
            Assert.Contains("closed every job", plan.IdleReason);
            Assert.DoesNotContain("every open job is at its cap", plan.IdleReason);
        }

        [Fact]
        public void IdleReasonSkipsMiningClauseWhenStoneHasNoOpenJob()
        {
            JobPlan plan = ColonyPlanner.Plan(Starving(20, cropTiles: 2), null, null);

            Assert.True(plan.Idle > 0, plan.Summary);
            Assert.StartsWith("capped, idle " + plan.Idle + ": every open job is at its cap", plan.IdleReason);
            Assert.DoesNotContain("mining waits", plan.IdleReason);
        }

        // Food share 70 leaves 3 farmer slots open, so mining waits; the surplus then fills them, and once
        // food and wood are full the rest goes to the ores instead of idling.
        [Fact]
        public void SurplusOpensStoneOnceFoodAndWoodAreFull()
        {
            JobPlan plan = ColonyPlanner.Plan(Starving(100, cropTiles: 72, ores: 30), null, null);

            Assert.Equal(72, plan.CountFor(JobIds.Farmer));
            Assert.Equal(1, plan.CountFor(JobIds.Hunter));
            Assert.Equal(27, plan.CountFor(JobIds.MinerDeposit));
            Assert.Equal(0, plan.Idle);
        }

        [Fact]
        public void SurplusStoneStopsAtItsCaps()
        {
            JobPlan plan = ColonyPlanner.Plan(Starving(100, cropTiles: 72, ores: 5), null, null);

            Assert.Equal(5, plan.CountFor(JobIds.MinerDeposit));
            Assert.Equal(22, plan.Idle);
            Assert.StartsWith("capped, idle 22: every open job is at its cap", plan.IdleReason);
        }

        [Fact]
        public void PlansNeverExceedAdultsAndReserveTheBuilderOverRandomSnapshots()
        {
            var random = new Random(Seed);
            for (int i = 0; i < 2000; i++)
            {
                int adults = random.Next(1, 80);
                int warriors = random.Next(0, adults + 2);
                CitySnapshot city = RandomCity(random, adults, warriors);

                JobPlan plan = ColonyPlanner.Plan(city, null, null);

                Assert.True(plan.TotalSlots <= adults, plan.Summary + " adults " + adults);
                Assert.Equal(adults, plan.TotalSlots + plan.Idle);
                if (city.PendingBuildings > 0 && warriors < adults)
                {
                    Assert.True(plan.CountFor(JobIds.Builder) >= 1, plan.Summary + " adults " + adults);
                }
            }
        }

        private static CitySnapshot RandomCity(Random random, int adults, int warriors) =>
            new CitySnapshot(
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
                baseSlots: new Dictionary<string, int> { [JobIds.Attacker] = warriors });
    }
}
