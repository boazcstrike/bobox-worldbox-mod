using System;
using System.Collections.Generic;
using BoBox.Planner;
using BoBox.Planner.Build;
using BoBox.Planner.Capacity;
using BoBox.Planner.Scenario;
using BoBox.Rules;
using Xunit;

namespace BoBox.Tests
{
    public class CapacityModelTests
    {
        private const int Seed = 20261108;

        private static Limits Default(AreaInfo area, Pathway pathway = null)
        {
            return CapacityModel.Limits(area, pathway ?? Pathway.Neutral, ColonyRules.Default);
        }

        [Fact]
        public void EveryJobLimitKeepsMinAtOrBelowMax()
        {
            var area = new AreaInfo(zones: 1, landTiles: 64, adults: 3, population: 5);

            Limits limits = CapacityModel.Limits(area, Pathway.Neutral, ColonyRules.Default);

            foreach (string jobId in ColonyRules.Default.Capacity.Jobs.Keys)
            {
                CountRange range = limits.ForJob(jobId);
                Assert.True(range.Min <= range.Max, jobId + " " + range);
            }

            Assert.Equal(CountRange.Unbounded, limits.ForJob(JobIds.Attacker));
        }

        [Fact]
        public void DefaultRangeAndUnknownIdsAreUnbounded()
        {
            Assert.Equal(CountRange.Unbounded, default(CountRange));
            Assert.Equal(CountRange.Unbounded, new CountRange());
            Assert.Equal(int.MaxValue, default(CountRange).Max);
            Assert.Equal(7, default(CountRange).Clamp(7));
            Assert.Equal(CountRange.Unbounded, Limits.Unbounded.ForJob("no_such_job"));
            Assert.Equal(CountRange.Unbounded, Default(new AreaInfo(zones: 5)).ForBuilding("type_unknown"));
        }

        [Fact]
        public void OneZoneCityGetsAtLeastOneSlotForEveryAreaJob()
        {
            Limits limits = Default(new AreaInfo(zones: 1, adults: 4, population: 6));

            Assert.Equal(new CountRange(0, 1), limits.ForJob(JobIds.Builder));
            Assert.Equal(2, limits.ForJob(JobIds.Farmer).Max);
            Assert.Equal(1, limits.ForJob(JobIds.GathererHoney).Max);
            Assert.Equal(new CountRange(1, 1), limits.ForBuilding(BuildingTypes.Stockpile));
        }

        [Fact]
        public void TwoHundredZoneCityHitsTheCeilings()
        {
            Limits limits = Default(new AreaInfo(zones: 200, adults: 300, population: 400));

            // Farmer ceiling grows with population: max(60, 0.4 x 400) = 160, under the area max of 400.
            Assert.Equal(160, limits.ForJob(JobIds.Farmer).Max);
            Assert.Equal(20, limits.ForJob(JobIds.Farmer).Min);
            Assert.Equal(8, limits.ForJob(JobIds.Builder).Max);
            Assert.Equal(new CountRange(40, 200), limits.ForBuilding(BuildingTypes.House));
        }

        [Fact]
        public void MaxGrowsWithLandAndLandTilesWinOverZones()
        {
            int small = Default(new AreaInfo(zones: 5)).ForJob(JobIds.Woodcutter).Max;
            int large = Default(new AreaInfo(zones: 20)).ForJob(JobIds.Woodcutter).Max;
            int halfWater = Default(new AreaInfo(zones: 20, landTiles: 10 * 64)).ForJob(JobIds.Woodcutter).Max;

            Assert.True(large > small);
            Assert.Equal(20, large);
            Assert.Equal(10, halfWater);
        }

        [Fact]
        public void MinGrowsWithPopulation()
        {
            int few = Default(new AreaInfo(zones: 200, adults: 10, population: 10)).ForJob(JobIds.Woodcutter).Min;
            int many = Default(new AreaInfo(zones: 200, adults: 100, population: 100)).ForJob(JobIds.Woodcutter).Min;

            Assert.Equal(0, few);
            Assert.Equal(5, many);
        }

        [Fact]
        public void ZeroAdultsMeansNoJobMinimumButBuildingsKeepTheirs()
        {
            Limits limits = Default(new AreaInfo(zones: 50, adults: 0, population: 40));

            foreach (string jobId in ColonyRules.Default.Capacity.Jobs.Keys)
            {
                Assert.Equal(0, limits.ForJob(jobId).Min);
            }

            Assert.Equal(4, limits.ForBuilding(BuildingTypes.House).Min);
            Assert.True(limits.ForJob(JobIds.Farmer).Max > 0);
        }

        [Fact]
        public void ZeroZonesMeansMaxWinsOverMinFloor()
        {
            Limits limits = Default(new AreaInfo(zones: 0, adults: 10, population: 50));

            Assert.Equal(new CountRange(0, 0), limits.ForBuilding(BuildingTypes.House));
            Assert.Equal(new CountRange(0, 0), limits.ForJob(JobIds.Farmer));
        }

        [Fact]
        public void PathwayMultiplierScalesMinAndMax()
        {
            var area = new AreaInfo(zones: 20, adults: 100, population: 100);
            var pathway = new Pathway("test", capacityMultipliers: new CapacityMultipliers(
                jobs: new Dictionary<string, double> { [JobIds.Woodcutter] = 2.0 },
                buildings: new Dictionary<string, double> { [BuildingTypes.House] = 0 }));

            Limits neutral = Default(area);
            Limits scaled = Default(area, pathway);

            Assert.Equal(new CountRange(5, 20), neutral.ForJob(JobIds.Woodcutter));
            Assert.Equal(new CountRange(10, 40), scaled.ForJob(JobIds.Woodcutter));
            Assert.Equal(new CountRange(0, 0), scaled.ForBuilding(BuildingTypes.House));
            Assert.Equal(neutral.ForJob(JobIds.Farmer), scaled.ForJob(JobIds.Farmer));
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void NonFiniteMultiplierCountsAsNeutral(double multiplier)
        {
            var area = new AreaInfo(zones: 20, adults: 100, population: 100);
            var pathway = new Pathway("broken", capacityMultipliers: new CapacityMultipliers(
                jobs: new Dictionary<string, double> { [JobIds.Woodcutter] = multiplier },
                buildings: new Dictionary<string, double> { [BuildingTypes.House] = multiplier }));

            Limits neutral = Default(area);
            Limits broken = Default(area, pathway);

            Assert.Equal(neutral.ForJob(JobIds.Woodcutter), broken.ForJob(JobIds.Woodcutter));
            Assert.Equal(neutral.ForBuilding(BuildingTypes.House), broken.ForBuilding(BuildingTypes.House));
        }

        [Fact]
        public void NegativeInfiniteMultiplierClosesTheJob()
        {
            var area = new AreaInfo(zones: 20, adults: 100, population: 100);
            var pathway = new Pathway("broken", capacityMultipliers: new CapacityMultipliers(
                jobs: new Dictionary<string, double> { [JobIds.Woodcutter] = double.NegativeInfinity },
                buildings: new Dictionary<string, double> { [BuildingTypes.House] = double.NegativeInfinity }));

            Limits limits = Default(area, pathway);

            Assert.Equal(new CountRange(0, 0), limits.ForJob(JobIds.Woodcutter));
            Assert.Equal(new CountRange(0, 0), limits.ForBuilding(BuildingTypes.House));
        }

        [Fact]
        public void LargerMapScalesMaxButNotMin()
        {
            Limits standard = Default(new AreaInfo(zones: 20, adults: 100, population: 100, mapSize: 4));
            Limits huge = Default(new AreaInfo(zones: 20, adults: 100, population: 100, mapSize: 16));

            Assert.Equal(40, standard.ForJob(JobIds.Farmer).Max);
            Assert.Equal(70, huge.ForJob(JobIds.Farmer).Max);
            Assert.Equal(standard.ForJob(JobIds.Farmer).Min, huge.ForJob(JobIds.Farmer).Min);
        }

        [Fact]
        public void NullAreaIsUnboundedAndNullRulesUseDefaults()
        {
            Assert.Same(Limits.Unbounded, CapacityModel.Limits(null, Pathway.Neutral, ColonyRules.Default));

            var area = new AreaInfo(zones: 12, adults: 30, population: 40);
            Limits fromNull = CapacityModel.Limits(area, null, null);
            Assert.Equal(Default(area).ForJob(JobIds.Farmer), fromNull.ForJob(JobIds.Farmer));
        }

        [Fact]
        public void MinNeverAboveMaxOverRandomCitiesRulesAndPathways()
        {
            var random = new Random(Seed);
            for (int i = 0; i < 2000; i++)
            {
                ColonyRules rules = i % 2 == 0 ? ColonyRules.Default : RandomRules(random);
                int adults = random.Next(0, 500);
                var area = new AreaInfo(
                    zones: random.Next(-2, 300),
                    landTiles: random.Next(0, 3) == 0 ? 0 : random.Next(0, 300 * 64),
                    adults: adults,
                    population: adults + random.Next(0, 200),
                    mapSize: random.Next(0, 20));

                Limits limits = CapacityModel.Limits(area, RandomPathway(random, rules), rules);

                AssertInvariant(limits.Jobs, rules.Capacity.Jobs.Count, adults);
                AssertInvariant(limits.Buildings, rules.Capacity.Buildings.Count, int.MaxValue);
            }
        }

        private static void AssertInvariant(IReadOnlyDictionary<string, CountRange> ranges, int expected, int minCap)
        {
            Assert.Equal(expected, ranges.Count);
            foreach (KeyValuePair<string, CountRange> pair in ranges)
            {
                Assert.True(pair.Value.Min >= 0 && pair.Value.Min <= pair.Value.Max, pair.Key + " " + pair.Value);
                Assert.True(pair.Value.Min <= minCap, pair.Key + " min above adults: " + pair.Value);
            }
        }

        private static ColonyRules RandomRules(Random random)
        {
            var jobs = new Dictionary<string, CapacityBase>();
            var buildings = new Dictionary<string, CapacityBase>();
            for (int i = 0; i < 6; i++)
            {
                jobs["job_" + i] = RandomBase(random);
                buildings["type_" + i] = RandomBase(random);
            }

            var mapScale = new[] { new MapScaleStep(0, random.NextDouble() * 2), new MapScaleStep(10, random.NextDouble() * 3) };
            return new ColonyRules(capacity: new CapacityRules(jobs, buildings), mapScale: mapScale);
        }

        private static CapacityBase RandomBase(Random random)
        {
            return new CapacityBase(
                minPerPop: random.NextDouble() * 2 - 0.2,
                maxPerZone: random.NextDouble() * 3 - 0.5,
                minFloor: random.Next(-3, 50),
                maxCeiling: random.Next(0, 4) == 0 ? CapacityBase.NoCeiling : random.Next(-5, 100));
        }

        private static Pathway RandomPathway(Random random, ColonyRules rules)
        {
            var jobs = new Dictionary<string, double>();
            foreach (string id in rules.Capacity.Jobs.Keys)
            {
                jobs[id] = random.NextDouble() * 4 - 0.5;
            }

            var buildings = new Dictionary<string, double>();
            foreach (string id in rules.Capacity.Buildings.Keys)
            {
                buildings[id] = random.NextDouble() * 4 - 0.5;
            }

            return new Pathway("random", capacityMultipliers: new CapacityMultipliers(jobs, buildings));
        }
    }
}
