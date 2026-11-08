using System.Collections.Generic;
using BoBox.Planner.Scenario;
using BoBox.Rules;
using Xunit;

namespace BoBox.Tests
{
    /// <summary>Pathway hysteresis, the coastal ocean share and the adaptive refresh budget.</summary>
    public class ScenarioHoldTests
    {
        private static readonly IReadOnlyList<Pathway> Pathways = ColonyRules.Default.Pathways;
        private const double RefreshSeconds = 8.0;
        private const double RefreshGap = 10.0;

        private static ScenarioResult Won(string pathwayId) =>
            new ScenarioResult(new[] { ScenarioTag.Rocky }, null, Find(pathwayId), "matched " + pathwayId);

        private static Pathway Find(string id)
        {
            foreach (Pathway pathway in Pathways)
            {
                if (pathway.Id == id)
                {
                    return pathway;
                }
            }

            return null;
        }

        // Runs refreshes in order, RefreshGap world seconds apart, and returns the pathway id in use after each one.
        private static List<string> Run(params string[] winners)
        {
            var times = new double[winners.Length];
            for (int i = 0; i < times.Length; i++)
            {
                times[i] = i * RefreshGap;
            }

            return RunAt(times, winners);
        }

        private static List<string> RunAt(double[] times, string[] winners)
        {
            var used = new List<string>();
            PathwayHold hold = PathwayHold.None;
            for (int i = 0; i < winners.Length; i++)
            {
                ScenarioResult fresh = Won(winners[i]);
                hold = hold.Next(fresh.Pathway.Id, Pathways, times[i], RefreshSeconds);
                used.Add(hold.Apply(fresh, Pathways).Pathway.Id);
            }

            return used;
        }

        [Fact]
        public void FirstWinnerIsAdoptedAtOnce()
        {
            Assert.Equal(new[] { "rocky" }, Run("rocky"));
        }

        [Fact]
        public void ChallengerNeedsTwoWinsInARow()
        {
            Assert.Equal(new[] { "rocky", "rocky", "forest", "forest" }, Run("rocky", "forest", "forest", "forest"));
        }

        [Fact]
        public void RapidWinsInsideRefreshSecondsDoNotSwitch()
        {
            var used = RunAt(new[] { 0.0, 1.0, 2.0, 3.0 }, new[] { "rocky", "forest", "forest", "forest" });

            Assert.Equal(new[] { "rocky", "rocky", "rocky", "rocky" }, used);
        }

        [Fact]
        public void WinsSpanningRefreshSecondsSwitchOnTheNextWin()
        {
            var used = RunAt(new[] { 0.0, 1.0, 2.0, 1.0 + RefreshSeconds }, new[] { "rocky", "forest", "forest", "forest" });

            Assert.Equal(new[] { "rocky", "rocky", "rocky", "forest" }, used);
        }

        [Fact]
        public void TimeGateCountsFromTheFirstWinOfTheStreak()
        {
            // The streak restarts at t=5 after a third id, so its first win is 5 and 12.9 is not yet 8 s later.
            var used = RunAt(new[] { 0.0, 1.0, 5.0, 6.0, 12.9, 13.0 }, new[] { "rocky", "forest", "fertile", "fertile", "fertile", "fertile" });

            Assert.Equal(new[] { "rocky", "rocky", "rocky", "rocky", "rocky", "fertile" }, used);
        }

        [Fact]
        public void AlternatingWinnersNeverFlip()
        {
            Assert.Equal(new[] { "rocky", "rocky", "rocky", "rocky", "rocky" }, Run("rocky", "forest", "rocky", "forest", "rocky"));
        }

        [Fact]
        public void ThirdPathwayResetsTheChallenger()
        {
            Assert.Equal(new[] { "rocky", "rocky", "rocky", "fertile" }, Run("rocky", "forest", "fertile", "fertile"));
        }

        [Fact]
        public void HeldResultKeepsFreshTagsAndExplainsTheHold()
        {
            PathwayHold hold = PathwayHold.None.Next("rocky", Pathways, 0, RefreshSeconds).Next("forest", Pathways, RefreshGap, RefreshSeconds);
            ScenarioResult fresh = Won("forest");

            ScenarioResult used = hold.Apply(fresh, Pathways);

            Assert.Equal("rocky", used.Pathway.Id);
            Assert.Equal(fresh.Tags, used.Tags);
            Assert.Contains("kept rocky until forest wins 2", used.Reason);
        }

        [Fact]
        public void HeldPathwayMissingFromTheRulesGivesWayAtOnce()
        {
            PathwayHold hold = PathwayHold.None.Next("rocky", Pathways, 0, RefreshSeconds);
            var reloaded = new List<Pathway> { Find("forest"), Find(Pathway.NeutralId) };

            PathwayHold next = hold.Next("forest", reloaded, 1, RefreshSeconds);

            Assert.Equal("forest", next.HeldId);
            Assert.Equal("forest", next.Apply(Won("forest"), reloaded).Pathway.Id);
        }

        [Fact]
        public void HeldPathwayComesFromTheCurrentRules()
        {
            var reloadedRocky = new Pathway("rocky", jobWeights: new Dictionary<string, double> { ["miner"] = 3.0 });
            var reloaded = new List<Pathway> { reloadedRocky, Find("forest") };
            PathwayHold hold = PathwayHold.None.Next("rocky", Pathways, 0, RefreshSeconds).Next("forest", reloaded, RefreshGap, RefreshSeconds);

            Assert.Same(reloadedRocky, hold.Apply(Won("forest"), reloaded).Pathway);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(11, false)]
        [InlineData(12, true)]
        [InlineData(50, true)]
        public void CoastFlagNeedsTheMinimumOceanShare(int oceanTiles, bool isCoastal)
        {
            // Ocean share = ocean / (land + ocean). With 100 land tiles, 11 ocean tiles is 0.099 (under the
            // default 0.1) and 12 is 0.107.
            var signals = new ScenarioSignals(zones: 10, landTiles: 100, trees: 10, minerals: 5, farmableTiles: 40,
                isCoastal: true, oceanTiles: oceanTiles);

            Assert.Equal(isCoastal, ScenarioClassifier.Classify(signals, ColonyRules.Default).Has(ScenarioTag.Coastal));
        }

        [Fact]
        public void OceanShareAtTheThresholdIsCoastal()
        {
            var signals = new ScenarioSignals(zones: 10, landTiles: 90, trees: 10, minerals: 5, farmableTiles: 40,
                isCoastal: true, oceanTiles: 10);

            Assert.Equal(0.1, signals.OceanShare, 9);
            Assert.True(ScenarioClassifier.Classify(signals, ColonyRules.Default).Has(ScenarioTag.Coastal));
        }

        [Fact]
        public void IslandBranchesIgnoreTheOceanShare()
        {
            var signals = new ScenarioSignals(zones: 10, landTiles: 100, trees: 10, minerals: 5, farmableTiles: 40,
                isCoastal: true, oceanTiles: 0, islandTiles: 1000, islandOwnedShare: 0.1);

            Assert.True(ScenarioClassifier.Classify(signals, ColonyRules.Default).Has(ScenarioTag.Coastal));
        }

        [Theory]
        [InlineData(0, 0.016, 2)]
        [InlineData(10, 0.016, 2)]
        [InlineData(5000, 0.016, 10)]
        [InlineData(40, 0.25, 2)]
        [InlineData(200, 0.25, 7)]
        [InlineData(288, 0.25, 9)]
        [InlineData(100, 0.0, 2)]
        [InlineData(100, 1000.0, 7)]
        public void RefreshBudgetCoversEveryCityPerInterval(int cities, double elapsed, int expected)
        {
            Assert.Equal(expected, RefreshBudget.For(cities, elapsed, new SignalRules(citiesPerFrame: 2, refreshSeconds: 8.0)));
        }

        [Fact]
        public void RefreshBudgetCountsAtMostHalfASecondOfElapsedTime()
        {
            var rules = new SignalRules(citiesPerFrame: 2, refreshSeconds: 8.0);

            Assert.Equal(RefreshBudget.For(100, RefreshBudget.MaxElapsedSeconds, rules), RefreshBudget.For(100, 1000.0, rules));
            Assert.True(RefreshBudget.For(100, 1000.0, rules) < SignalBounds.MaxCitiesPerFrame);
        }

        [Fact]
        public void RefreshBudgetClampsTheConfiguredFloor()
        {
            Assert.Equal(SignalBounds.MaxCitiesPerFrame, RefreshBudget.For(1, 0.016, new SignalRules(citiesPerFrame: 50)));
            Assert.Equal(SignalBounds.MinCitiesPerFrame, RefreshBudget.For(1, 0.016, new SignalRules(citiesPerFrame: 0)));
        }
    }
}
