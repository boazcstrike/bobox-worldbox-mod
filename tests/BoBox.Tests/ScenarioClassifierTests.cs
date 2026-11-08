using System.Collections.Generic;
using BoBox.Planner.Scenario;
using BoBox.Rules;
using Xunit;

namespace BoBox.Tests
{
    public class ScenarioClassifierTests
    {
        private static readonly ColonyRules Rules = ColonyRules.Default;

        // A city that matches no tag: 10 zones, 100 land tiles, middling resources, no ring, no island.
        private static ScenarioSignals City(
            int trees = 10, int minerals = 5, int farmableTiles = 40, int hillsTiles = 0, int mountainTiles = 0,
            int hostileBiomeTiles = 0, int snowTiles = 0, int sandTiles = 0, int harshTiles = 0,
            bool isCoastal = false, int oceanTiles = 0, int islandTiles = 0, double islandOwnedShare = 0,
            int ringZones = 0, int ringUnclaimedZones = 0, int ringForeignZones = 0, int freeZonesInRange = 0,
            int hostileNeighbourKingdoms = 0, int nearestForeignCityZones = ScenarioSignals.NoForeignCity,
            int zones = 10, int landTiles = 100)
        {
            return new ScenarioSignals(
                zones: zones, landTiles: landTiles, trees: trees, minerals: minerals,
                farmableTiles: farmableTiles, hillsTiles: hillsTiles, mountainTiles: mountainTiles,
                hostileBiomeTiles: hostileBiomeTiles, snowTiles: snowTiles, sandTiles: sandTiles, harshTiles: harshTiles,
                isCoastal: isCoastal, oceanTiles: oceanTiles, islandTiles: islandTiles, islandOwnedShare: islandOwnedShare,
                ringZones: ringZones, ringUnclaimedZones: ringUnclaimedZones, ringForeignZones: ringForeignZones,
                freeZonesInRange: freeZonesInRange, hostileNeighbourKingdoms: hostileNeighbourKingdoms,
                nearestForeignCityZones: nearestForeignCityZones);
        }

        private static ScenarioResult Classify(ScenarioSignals signals) => ScenarioClassifier.Classify(signals, Rules);

        [Fact]
        public void EmptyCityGetsTheDefaultPathway()
        {
            ScenarioResult result = Classify(ScenarioSignals.Empty);

            Assert.Empty(result.Tags);
            Assert.Equal(Rules.DefaultPathway.Id, result.Pathway.Id);
            Assert.Contains("pathway: default", result.Reason);
        }

        [Fact]
        public void ZeroZonesGivesNoTagsEvenWithData()
        {
            ScenarioResult result = Classify(City(zones: 0, trees: 0, minerals: 0, farmableTiles: 0, isCoastal: true, oceanTiles: 50));

            Assert.Empty(result.Tags);
        }

        [Fact]
        public void ForestRichMatchesAtTwoTreesPerZone()
        {
            ScenarioResult result = Classify(City(trees: 25));

            Assert.True(result.Has(ScenarioTag.ForestRich));
            Assert.Equal("forest", result.Pathway.Id);
        }

        [Fact]
        public void ForestRichMissesBelowTwoTreesPerZone()
        {
            Assert.False(Classify(City(trees: 19)).Has(ScenarioTag.ForestRich));
        }

        [Fact]
        public void FertilePlainsMatchesWithManyFarmableTilesAndLittleRock()
        {
            ScenarioResult result = Classify(City(farmableTiles: 70, hillsTiles: 5));

            Assert.True(result.Has(ScenarioTag.FertilePlains));
            Assert.Equal("fertile", result.Pathway.Id);
        }

        [Fact]
        public void FertilePlainsMissesWhenRockIsTooHigh()
        {
            Assert.False(Classify(City(farmableTiles: 70, hillsTiles: 8, mountainTiles: 4)).Has(ScenarioTag.FertilePlains));
        }

        [Fact]
        public void FertilePlainsMissesWhenFarmableLandIsLow()
        {
            Assert.False(Classify(City(farmableTiles: 50)).Has(ScenarioTag.FertilePlains));
        }

        [Fact]
        public void RockyMatchesOnRockShare()
        {
            ScenarioResult result = Classify(City(hillsTiles: 20, mountainTiles: 10));

            Assert.True(result.Has(ScenarioTag.Rocky));
            Assert.Equal("rocky", result.Pathway.Id);
        }

        [Fact]
        public void RockyMatchesOnMineralsPerZoneAlone()
        {
            Assert.True(Classify(City(minerals: 10)).Has(ScenarioTag.Rocky));
        }

        [Fact]
        public void RockyMissesWithLittleRockAndFewMinerals()
        {
            Assert.False(Classify(City(hillsTiles: 10, minerals: 9)).Has(ScenarioTag.Rocky));
        }

        [Fact]
        public void ScarceMatchesWhenTreesMineralsAndFarmlandAreAllLow()
        {
            ScenarioResult result = Classify(City(trees: 0, minerals: 0, farmableTiles: 10));

            Assert.True(result.Has(ScenarioTag.Scarce));
            Assert.Equal("scarce", result.Pathway.Id);
        }

        [Fact]
        public void ScarceMatchesOnHarshGroundAlone()
        {
            Assert.True(Classify(City(snowTiles: 20, sandTiles: 10, hostileBiomeTiles: 10, harshTiles: 40)).Has(ScenarioTag.Scarce));
        }

        [Fact]
        public void ScarceMissesWhenOneResourceIsPlentiful()
        {
            Assert.False(Classify(City(trees: 0, minerals: 0, farmableTiles: 40)).Has(ScenarioTag.Scarce));
        }

        [Fact]
        public void CoastalMatchesWhenCoastalFlagIsSet()
        {
            ScenarioResult result = Classify(City(isCoastal: true, oceanTiles: 50));

            Assert.True(result.Has(ScenarioTag.Coastal));
            Assert.Equal("coastal", result.Pathway.Id);
        }

        [Fact]
        public void CoastalMatchesOnSmallIslandOrOwnedShare()
        {
            Assert.True(Classify(City(islandTiles: 1000, islandOwnedShare: 0.1)).Has(ScenarioTag.Coastal));
            Assert.True(Classify(City(islandTiles: 100000, islandOwnedShare: 0.8)).Has(ScenarioTag.Coastal));
        }

        [Fact]
        public void CoastalMissesInlandOnABigIslandWithLittleShare()
        {
            Assert.False(Classify(City(islandTiles: 100000, islandOwnedShare: 0.1)).Has(ScenarioTag.Coastal));
            Assert.False(Classify(City()).Has(ScenarioTag.Coastal));
        }

        [Fact]
        public void CrowdedFrontierMatchesOnEachOfItsThreeConditions()
        {
            Assert.True(Classify(City(ringZones: 10, ringForeignZones: 4)).Has(ScenarioTag.CrowdedFrontier));
            Assert.True(Classify(City(nearestForeignCityZones: 4)).Has(ScenarioTag.CrowdedFrontier));
            Assert.True(Classify(City(hostileNeighbourKingdoms: 1)).Has(ScenarioTag.CrowdedFrontier));
        }

        [Fact]
        public void CrowdedFrontierMissesWhenNeighboursAreFarAndFriendly()
        {
            ScenarioResult result = Classify(City(ringZones: 10, ringForeignZones: 2, nearestForeignCityZones: 7));

            Assert.False(result.Has(ScenarioTag.CrowdedFrontier));
        }

        [Fact]
        public void NoForeignCitySentinelNeverCountsAsNearEvenWithAHugeMaxDistance()
        {
            var thresholds = new ScenarioThresholds(
                crowdedFrontier: new CrowdedFrontierThresholds(maxForeignCityDistanceZones: int.MaxValue));

            ScenarioResult result = ScenarioClassifier.Classify(City(), new ColonyRules(scenarios: thresholds));

            Assert.False(result.Has(ScenarioTag.CrowdedFrontier));
        }

        [Fact]
        public void PathwayConditionTreatsNullTagsAsNoTags()
        {
            Assert.False(new PathwayCondition(null, new[] { ScenarioTag.Rocky }).Matches(null));
            Assert.True(PathwayCondition.Always.Matches(null));
        }

        [Fact]
        public void OpenFrontierMatchesWithUnclaimedRingAndFreeZones()
        {
            ScenarioResult result = Classify(City(ringZones: 10, ringUnclaimedZones: 8, freeZonesInRange: 20));

            Assert.True(result.Has(ScenarioTag.OpenFrontier));
            Assert.Equal("open_frontier", result.Pathway.Id);
        }

        [Fact]
        public void OpenFrontierNeedsBothConditions()
        {
            Assert.False(Classify(City(ringZones: 10, ringUnclaimedZones: 8, freeZonesInRange: 9)).Has(ScenarioTag.OpenFrontier));
            Assert.False(Classify(City(ringZones: 10, ringUnclaimedZones: 4, freeZonesInRange: 20)).Has(ScenarioTag.OpenFrontier));
        }

        [Fact]
        public void ScarceBeatsForestRichByPathwayOrder()
        {
            // Half the land is hostile (scarce) and there are 3 trees per zone (forest rich).
            ScenarioResult result = Classify(City(trees: 30, hostileBiomeTiles: 50, harshTiles: 50));

            Assert.True(result.Has(ScenarioTag.Scarce));
            Assert.True(result.Has(ScenarioTag.ForestRich));
            Assert.Equal("scarce", result.Pathway.Id);
        }

        [Fact]
        public void CrowdedBeatsOpenAndCoastalByPathwayOrder()
        {
            ScenarioResult result = Classify(City(
                isCoastal: true, oceanTiles: 50, hostileNeighbourKingdoms: 1, ringZones: 10, ringUnclaimedZones: 8, freeZonesInRange: 20));

            Assert.Equal("crowded", result.Pathway.Id);
        }

        [Fact]
        public void ThresholdIsInclusiveForMinimumsAndMaximums()
        {
            // 20 trees on 10 zones is exactly 2 per zone; 0.5 trees per zone and the other maximums are scarce limits.
            Assert.True(Classify(City(trees: 20)).Has(ScenarioTag.ForestRich));
            Assert.True(Classify(City(trees: 5, minerals: 2, farmableTiles: 20)).Has(ScenarioTag.Scarce));
            Assert.False(Classify(City(trees: 6, minerals: 2, farmableTiles: 20)).Has(ScenarioTag.Scarce));
        }

        [Fact]
        public void BelowThresholdsAreExclusive()
        {
            // Rock share exactly 10% blocks fertile plains; an island of exactly 2500 tiles is not small.
            Assert.False(Classify(City(farmableTiles: 70, hillsTiles: 10)).Has(ScenarioTag.FertilePlains));
            Assert.True(Classify(City(farmableTiles: 70, hillsTiles: 9)).Has(ScenarioTag.FertilePlains));
            Assert.False(Classify(City(islandTiles: 2500, islandOwnedShare: 0.1)).Has(ScenarioTag.Coastal));
            Assert.True(Classify(City(islandTiles: 2499, islandOwnedShare: 0.1)).Has(ScenarioTag.Coastal));
        }

        [Fact]
        public void ScoreIsHalfAtTheThresholdAndOneAtDoubleIt()
        {
            ScenarioResult atThreshold = Classify(City(trees: 20));
            ScenarioResult atDouble = Classify(City(trees: 40));

            Assert.Equal(0.5, atThreshold.Scores[ScenarioTag.ForestRich], 6);
            Assert.Equal(1.0, atDouble.Scores[ScenarioTag.ForestRich], 6);
            Assert.Equal(0.0, Classify(City()).Scores[ScenarioTag.ForestRich]);
        }

        [Fact]
        public void EveryTagIsScoredBetweenZeroAndOne()
        {
            ScenarioResult result = Classify(City(trees: 400, minerals: 400, isCoastal: true, oceanTiles: 50, hostileNeighbourKingdoms: 5));

            Assert.Equal(7, result.Scores.Count);
            foreach (KeyValuePair<ScenarioTag, double> pair in result.Scores)
            {
                Assert.InRange(pair.Value, 0.0, 1.0);
            }
        }

        [Fact]
        public void ZeroDivisorsGiveNoNaN()
        {
            // Zones > 0 with no land and no ring: every ratio has a zero divisor.
            ScenarioResult result = Classify(City(landTiles: 0, ringZones: 0));

            foreach (KeyValuePair<ScenarioTag, double> pair in result.Scores)
            {
                Assert.False(double.IsNaN(pair.Value));
                Assert.InRange(pair.Value, 0.0, 1.0);
            }
        }

        [Fact]
        public void NoPathwayMatchFallsBackToTheRulesDefault()
        {
            var rules = new ColonyRules(
                pathways: new[] { new Pathway("only_rocky", new PathwayCondition(null, new[] { ScenarioTag.Rocky })) });

            ScenarioResult result = ScenarioClassifier.Classify(City(trees: 30), rules);

            Assert.Equal(Pathway.NeutralId, result.Pathway.Id);
        }

        [Fact]
        public void ReasonNamesTagsScoresPathwayAndCondition()
        {
            ScenarioResult result = Classify(City(trees: 30, isCoastal: true, oceanTiles: 50));

            Assert.Contains("forest_rich=", result.Reason);
            Assert.Contains("coastal=", result.Reason);
            Assert.Contains("pathway: coastal", result.Reason);
            Assert.Contains("any of [coastal]", result.Reason);
        }
    }
}
