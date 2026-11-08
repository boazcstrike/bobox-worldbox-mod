using System.Collections.Generic;
using BoBox.Planner.Scenario;
using BoBox.Rules;
using Xunit;

namespace BoBox.Tests
{
    /// <summary>Edge-hunt fixes C1, C2, C3, C5 and C6: undefined tags, renamed default, double-counted harsh tiles, rewound clock, null ids.</summary>
    public class ScenarioEdgeCaseTests
    {
        private const double RefreshSeconds = 8.0;
        private const ScenarioTag UndefinedTag = (ScenarioTag)99;

        private static Pathway Always(string id) => new Pathway(id);

        private static Pathway When(string id, params ScenarioTag[] any) =>
            new Pathway(id, new PathwayCondition(any: any));

        [Fact]
        public void UndefinedTagInAnyConditionNeverThrows()
        {
            ColonyRules rules = ColonyRulesWith(
                new Pathway("odd", new PathwayCondition(any: new[] { UndefinedTag, ScenarioTag.Rocky })), Always("default"));
            var rocky = new ScenarioSignals(zones: 10, landTiles: 100, hillsTiles: 60);

            ScenarioResult result = ScenarioClassifier.Classify(rocky, rules);

            Assert.Equal("odd", result.Pathway.Id);
            Assert.Contains("any of [rocky]", result.Reason);
            Assert.DoesNotContain("99", result.Reason);
        }

        [Fact]
        public void UndefinedTagInRequiredConditionFallsThroughWithoutThrowing()
        {
            ColonyRules rules = ColonyRulesWith(
                new Pathway("odd", new PathwayCondition(required: new[] { UndefinedTag })), Always("default"));

            ScenarioResult result = ScenarioClassifier.Classify(new ScenarioSignals(zones: 10, landTiles: 100), rules);

            Assert.Equal("default", result.Pathway.Id);
        }

        [Fact]
        public void ConditionMadeOfUndefinedTagsFallsBackToDefault()
        {
            ColonyRules rules = ColonyRulesWith(new Pathway("odd", new PathwayCondition(any: new[] { UndefinedTag })));

            ScenarioResult result = ScenarioClassifier.Classify(new ScenarioSignals(), rules);

            Assert.Equal("default", result.Pathway.Id);
            Assert.DoesNotContain("99", result.Reason);
        }

        [Fact]
        public void RenamedDefaultPathwayGivesWayAtOnce()
        {
            var pathways = new List<Pathway> { When("scarce", ScenarioTag.Scarce), Always("fallback") };
            var holding = new PathwayHold("default", null, 0, 0);

            PathwayHold next = holding.Next("fallback", pathways, now: 0, minSeconds: RefreshSeconds);

            Assert.Equal("fallback", next.HeldId);
        }

        [Fact]
        public void NeutralStillCountsAsListedWhenNoPathwayAlwaysMatches()
        {
            var pathways = new List<Pathway> { When("scarce", ScenarioTag.Scarce) };
            var holding = new PathwayHold(Pathway.NeutralId, null, 0, 0);

            PathwayHold next = holding.Next("scarce", pathways, now: 0, minSeconds: RefreshSeconds);

            Assert.Equal(Pathway.NeutralId, next.HeldId);
            Assert.Equal("scarce", next.ChallengerId);
        }

        [Fact]
        public void HarshShareCountsEachTileOnce()
        {
            var signals = new ScenarioSignals(
                zones: 10, landTiles: 100, hostileBiomeTiles: 100, snowTiles: 100, sandTiles: 100, harshTiles: 100);

            Assert.InRange(signals.HarshShare, 0.0, 1.0);
            Assert.Equal(1.0, signals.HarshShare, 6);
        }

        [Fact]
        public void HarshShareReadsTheCombinedCountNotTheLayerSums()
        {
            var signals = new ScenarioSignals(
                zones: 10, landTiles: 100, hostileBiomeTiles: 30, sandTiles: 30, harshTiles: 30);

            Assert.Equal(0.3, signals.HarshShare, 6);
        }

        [Fact]
        public void HarshShareStaysWithinOneWhenCountsExceedTheDenominator()
        {
            var signals = new ScenarioSignals(zones: 10, landTiles: 10, harshTiles: 500);

            Assert.Equal(1.0, signals.HarshShare, 6);
        }

        [Fact]
        public void ChallengerStreakRestartsWhenWorldTimeMovesBack()
        {
            var pathways = new List<Pathway> { When("scarce", ScenarioTag.Scarce), Always("default") };
            var streak = new PathwayHold("default", "scarce", 1, 1000);

            PathwayHold next = streak.Next("scarce", pathways, now: 10, minSeconds: RefreshSeconds);

            Assert.Equal("default", next.HeldId);
            Assert.Equal("scarce", next.ChallengerId);
            Assert.Equal(1, next.ChallengerWins);
            Assert.Equal(10, next.ChallengerSince);
        }

        [Fact]
        public void NullIdGivesNeutralMultiplier()
        {
            var multipliers = new CapacityMultipliers(
                new Dictionary<string, double> { ["farmer"] = 2.0 }, new Dictionary<string, double> { ["type_house"] = 3.0 });

            Assert.Equal(CapacityMultipliers.Neutral, multipliers.ForJob(null));
            Assert.Equal(CapacityMultipliers.Neutral, multipliers.ForBuilding(null));
            Assert.Equal(2.0, multipliers.ForJob("farmer"));
            Assert.Equal(3.0, multipliers.ForBuilding("type_house"));
            Assert.Equal(CapacityMultipliers.Neutral, CapacityMultipliers.None.ForJob(null));
        }

        // The default rules with only the given pathways, so a test controls the whole list.
        private static ColonyRules ColonyRulesWith(params Pathway[] pathways)
        {
            return ColonyRules.Default.WithPathways(pathways);
        }
    }
}
