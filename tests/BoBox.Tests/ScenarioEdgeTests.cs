using System.Collections.Generic;
using BoBox.Planner.Scenario;
using BoBox.Rules;
using Xunit;

namespace BoBox.Tests
{
    /// <summary>Bad input, exact threshold edges, pathway order and null guards for the scenario classifier.</summary>
    public class ScenarioEdgeTests
    {
        private static readonly ColonyRules Rules = ColonyRules.Default;

        // Same baseline as ScenarioClassifierTests: 10 zones, 100 land tiles, matches no tag.
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

        private static void AssertFiniteScores(ScenarioResult result)
        {
            foreach (KeyValuePair<ScenarioTag, double> pair in result.Scores)
            {
                Assert.False(double.IsNaN(pair.Value));
                Assert.InRange(pair.Value, 0.0, 1.0);
            }
        }

        public static IEnumerable<object[]> BadInputCities()
        {
            yield return new object[] { "island share NaN", City(islandTiles: 100000, islandOwnedShare: double.NaN) };
            yield return new object[] { "island share -1", City(islandTiles: 100000, islandOwnedShare: -1) };
            yield return new object[] { "negative trees", City(trees: -50) };
            yield return new object[] { "negative minerals", City(minerals: -50) };
            yield return new object[] { "negative farmable", City(farmableTiles: -50) };
            yield return new object[] { "negative hills and mountains", City(hillsTiles: -30, mountainTiles: -30) };
            yield return new object[] { "negative land tiles", City(landTiles: -100) };
            yield return new object[] { "negative ring foreign", City(ringZones: 10, ringForeignZones: -5) };
            yield return new object[] { "negative ring unclaimed", City(ringZones: 10, ringUnclaimedZones: -8, freeZonesInRange: 20) };
            yield return new object[] { "negative free zones", City(ringZones: 10, ringUnclaimedZones: 8, freeZonesInRange: -20) };
            yield return new object[] { "negative hostile neighbours", City(hostileNeighbourKingdoms: -3) };
            yield return new object[] { "negative island tiles", City(islandTiles: -5, islandOwnedShare: 0.9) };
            yield return new object[] { "negative foreign distance", City(nearestForeignCityZones: -1) };
            yield return new object[] { "negative zones", City(zones: -10) };
        }

        [Theory]
        [MemberData(nameof(BadInputCities))]
        public void BadInputGivesNoNaNAndNoSpuriousTag(string label, ScenarioSignals signals)
        {
            ScenarioResult result = Classify(signals);

            AssertFiniteScores(result);
            Assert.True(result.Tags.Count == 0, label + " produced " + result.Reason);
        }

        [Fact]
        public void NegativeCountDoesNotCancelARealOneInAShareSum()
        {
            Assert.Equal(0.0, City(harshTiles: -50).HarshShare, 6);
            Assert.False(Classify(City(harshTiles: -50)).Has(ScenarioTag.Scarce));
            Assert.Equal(50 / 1050.0, City(hillsTiles: -10, mountainTiles: 50, landTiles: 1000).RockShare, 6);
        }

        [Fact]
        public void MountainsCountInTheShareDenominatorSoSharesStayWithinOne()
        {
            // Mountains and summit have no ground flag, so LandTiles (tiles_with_ground) excludes them.
            Assert.Equal(0.25, City(mountainTiles: 16, landTiles: 48).RockShare, 6);
            Assert.Equal(64, City(mountainTiles: 16, landTiles: 48).SolidTiles);
            Assert.InRange(City(mountainTiles: 100, landTiles: 1).RockShare, 0.0, 1.0);
            Assert.Equal(1.0, City(mountainTiles: 100, landTiles: 0).RockShare, 6);
            Assert.InRange(City(mountainTiles: 100, harshTiles: 100, landTiles: 1).HarshShare, 0.0, 1.0);
        }

        [Fact]
        public void SnowyMountainCityIsRockyNotScarce()
        {
            ScenarioResult result = Classify(City(mountainTiles: 60, harshTiles: 45, landTiles: 100));

            Assert.Equal(45 / 160.0, City(mountainTiles: 60, harshTiles: 45, landTiles: 100).HarshShare, 6);
            Assert.True(result.Has(ScenarioTag.Rocky));
            Assert.False(result.Has(ScenarioTag.Scarce));
        }

        [Fact]
        public void NegativeForeignCityDistanceIsNotAForeignCity()
        {
            Assert.False(City(nearestForeignCityZones: -1).HasForeignCity);
            Assert.True(City(nearestForeignCityZones: 0).HasForeignCity);
            Assert.False(Classify(City(nearestForeignCityZones: -1)).Has(ScenarioTag.CrowdedFrontier));
        }

        [Fact]
        public void InfiniteThresholdsGiveNoNaN()
        {
            double inf = double.PositiveInfinity;
            var thresholds = new ScenarioThresholds(
                scarce: new ScarceThresholds(maxTreesPerZone: inf, maxMineralsPerZone: inf, maxFarmableShare: inf));

            ScenarioResult result = ScenarioClassifier.Classify(City(), new ColonyRules(scenarios: thresholds));

            AssertFiniteScores(result);
            Assert.Equal(ScenarioScoring.AtThresholdScore, result.Scores[ScenarioTag.Scarce], 6);
            Assert.Equal(ScenarioScoring.AtThresholdScore, ScenarioScoring.AtLeast(inf, inf), 6);
            Assert.Equal(ScenarioScoring.AtThresholdScore, ScenarioScoring.AtMost(0, inf), 6);
            Assert.Equal(ScenarioScoring.AtThresholdScore, ScenarioScoring.Below(0, inf), 6);
        }

        [Fact]
        public void ZeroZonesGetsTheDefaultPathway()
        {
            ScenarioResult result = Classify(City(zones: 0, isCoastal: true, oceanTiles: 50, trees: 50));

            Assert.Empty(result.Tags);
            Assert.Equal(Rules.DefaultPathway.Id, result.Pathway.Id);
            Assert.Contains("pathway: default", result.Reason);
        }

        [Fact]
        public void NullJobAndBuildingLookupsAreNeutral()
        {
            var pathway = new Pathway(
                "p",
                jobWeights: new Dictionary<string, double> { { "job_farmer", 3.0 } },
                buildingPriority: new Dictionary<string, int> { { "type_house", 5 } });

            Assert.Equal(Pathway.NeutralWeight, pathway.WeightForJob(null));
            Assert.Equal(Pathway.NeutralPriority, pathway.PriorityForBuilding(null));
            Assert.Equal(Pathway.NeutralWeight, Pathway.Neutral.WeightForJob(null));
            Assert.Equal(Pathway.NeutralPriority, Pathway.Neutral.PriorityForBuilding(null));
            Assert.Equal(3.0, pathway.WeightForJob("job_farmer"));
            Assert.Equal(5, pathway.PriorityForBuilding("type_house"));
            Assert.Equal(Pathway.NeutralPriority, pathway.PriorityForBuilding("type_missing"));
        }

        public static IEnumerable<object[]> AdjacentPairs()
        {
            yield return new object[] { City(isCoastal: true, oceanTiles: 50, minerals: 10), ScenarioTag.Coastal, ScenarioTag.Rocky, "coastal" };
            yield return new object[] { City(minerals: 10, trees: 30), ScenarioTag.Rocky, ScenarioTag.ForestRich, "rocky" };
            yield return new object[] { City(trees: 30, farmableTiles: 70), ScenarioTag.ForestRich, ScenarioTag.FertilePlains, "forest" };
            yield return new object[]
            {
                City(farmableTiles: 70, ringZones: 10, ringUnclaimedZones: 8, freeZonesInRange: 20),
                ScenarioTag.FertilePlains, ScenarioTag.OpenFrontier, "fertile"
            };
        }

        [Theory]
        [MemberData(nameof(AdjacentPairs))]
        public void EarlierPathwayWinsOverTheNextOne(
            ScenarioSignals signals, ScenarioTag winnerTag, ScenarioTag loserTag, string winnerId)
        {
            ScenarioResult result = Classify(signals);

            Assert.True(result.Has(winnerTag));
            Assert.True(result.Has(loserTag));
            Assert.Equal(winnerId, result.Pathway.Id);
        }

        public static IEnumerable<object[]> ThresholdEdges()
        {
            // FertilePlains: farmable share min 0.6.
            yield return new object[] { City(farmableTiles: 60), ScenarioTag.FertilePlains, true };
            yield return new object[] { City(farmableTiles: 59), ScenarioTag.FertilePlains, false };
            // Rocky: rock share (over ground plus mountain tiles) min 0.25, minerals per zone min 1.0.
            yield return new object[] { City(hillsTiles: 15, mountainTiles: 10, landTiles: 90), ScenarioTag.Rocky, true };
            yield return new object[] { City(hillsTiles: 14, mountainTiles: 10, landTiles: 90), ScenarioTag.Rocky, false };
            yield return new object[] { City(minerals: 10), ScenarioTag.Rocky, true };
            yield return new object[] { City(minerals: 9), ScenarioTag.Rocky, false };
            // Scarce: minerals per zone max 0.25, farmable share max 0.2, harsh share min 0.4.
            yield return new object[] { City(zones: 20, trees: 10, minerals: 5, farmableTiles: 20), ScenarioTag.Scarce, true };
            yield return new object[] { City(zones: 20, trees: 10, minerals: 6, farmableTiles: 20), ScenarioTag.Scarce, false };
            yield return new object[] { City(zones: 20, trees: 11, minerals: 5, farmableTiles: 20), ScenarioTag.Scarce, false };
            yield return new object[] { City(trees: 5, minerals: 2, farmableTiles: 21), ScenarioTag.Scarce, false };
            yield return new object[] { City(harshTiles: 40), ScenarioTag.Scarce, true };
            yield return new object[] { City(harshTiles: 39), ScenarioTag.Scarce, false };
            // Coastal: island owned share min 0.5.
            yield return new object[] { City(islandTiles: 100000, islandOwnedShare: 0.5), ScenarioTag.Coastal, true };
            yield return new object[] { City(islandTiles: 100000, islandOwnedShare: 0.49), ScenarioTag.Coastal, false };
            // CrowdedFrontier: foreign ring share min 0.3, distance max 6, hostile neighbours min 1.
            yield return new object[] { City(ringZones: 10, ringForeignZones: 3), ScenarioTag.CrowdedFrontier, true };
            yield return new object[] { City(ringZones: 10, ringForeignZones: 2), ScenarioTag.CrowdedFrontier, false };
            yield return new object[] { City(nearestForeignCityZones: 6), ScenarioTag.CrowdedFrontier, true };
            yield return new object[] { City(nearestForeignCityZones: 7), ScenarioTag.CrowdedFrontier, false };
            yield return new object[] { City(hostileNeighbourKingdoms: 1), ScenarioTag.CrowdedFrontier, true };
            yield return new object[] { City(hostileNeighbourKingdoms: 0), ScenarioTag.CrowdedFrontier, false };
            // OpenFrontier: unclaimed ring share min 0.5, free zones in range min 10.
            yield return new object[] { City(ringZones: 10, ringUnclaimedZones: 5, freeZonesInRange: 10), ScenarioTag.OpenFrontier, true };
            yield return new object[] { City(ringZones: 10, ringUnclaimedZones: 5, freeZonesInRange: 9), ScenarioTag.OpenFrontier, false };
            yield return new object[] { City(ringZones: 10, ringUnclaimedZones: 4, freeZonesInRange: 10), ScenarioTag.OpenFrontier, false };
        }

        [Theory]
        [MemberData(nameof(ThresholdEdges))]
        public void ThresholdEdgeMatchesExactlyAtTheValueAndMissesJustPast(
            ScenarioSignals signals, ScenarioTag tag, bool isExpected)
        {
            Assert.Equal(isExpected, Classify(signals).Has(tag));
        }
    }
}
