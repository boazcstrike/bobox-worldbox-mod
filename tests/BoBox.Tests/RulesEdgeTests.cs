using System;
using System.Linq;
using BoBox.Planner.Scenario;
using BoBox.Rules;
using Xunit;

namespace BoBox.Tests
{
    public class RulesEdgeTests
    {
        private const string Source = "test.json";
        private const string PreviousFile = "C:\\mods\\bobox-rules.json";

        private static string ForestRichMinimum(string value) =>
            "{\"scenarios\":{\"forest_rich\":{\"min_trees_per_zone\":" + value + "}}}";

        private static string PathwayWithAny(string tags) =>
            "{\"pathways\":[{\"id\":\"a\",\"when\":{\"any\":[" + tags + "]}},{\"id\":\"default\"}]}";

        [Theory]
        [InlineData("12345678901234567890")]
        [InlineData("5000000000")]
        [InlineData("\"abc\"")]
        public void AnUnusableVersionFailsTheLoadWithoutThrowing(string version)
        {
            RulesLoadResult result = null;
            Exception thrown = Record.Exception(() => result = RulesLoader.Parse("{\"version\":" + version + "}", Source));

            Assert.Null(thrown);
            Assert.False(result.HasRules);
            Assert.NotEmpty(result.Errors);
        }

        [Theory]
        [InlineData("\"1e9\"", 1000.0)]
        [InlineData("\"5000\"", 1000.0)]
        [InlineData("\"Infinity\"", 1000.0)]
        [InlineData("\"-Infinity\"", 0.0)]
        [InlineData("\"NaN\"", 2.0)]
        public void ANumericStringIsClampedLikeANumber(string value, double expected)
        {
            RulesLoadResult result = RulesLoader.Parse(ForestRichMinimum(value), Source);

            Assert.True(result.HasRules, string.Join("; ", result.Errors));
            Assert.Equal(expected, result.Rules.Scenarios.ForestRich.MinTreesPerZone);
            string warning = Assert.Single(result.Warnings, w => w.Contains("min_trees_per_zone") && w.Contains("clamped"));
            Assert.Contains("clamped to", warning);
        }

        [Fact]
        public void ANumericStringInRangeLoadsAsTheNumberWithoutAWarning()
        {
            RulesLoadResult result = RulesLoader.Parse(ForestRichMinimum("\"7.5\""), Source);

            Assert.True(result.HasRules);
            Assert.Equal(7.5, result.Rules.Scenarios.ForestRich.MinTreesPerZone);
            Assert.Empty(result.Warnings);
        }

        [Fact]
        public void AnIntegerFieldGivenAStringIsClampedToTheLimit()
        {
            RulesLoadResult result = RulesLoader.Parse("{\"signals\":{\"cities_per_frame\":\"5000\"}}", Source);

            Assert.True(result.HasRules, string.Join("; ", result.Errors));
            Assert.Equal(SignalBounds.MaxCitiesPerFrame, result.Rules.Signals.CitiesPerFrame);
        }

        [Fact]
        public void UnknownIdsInPathwayMapsWarn()
        {
            RulesLoadResult result = RulesLoader.Parse(
                "{\"pathways\":[{\"id\":\"a\",\"when\":{\"any\":[\"coastal\"]},\"job_weights\":{\"farmr\":1},\"building_priority\":{\"type_nope\":1},"
                + "\"capacity_multipliers\":{\"jobs\":{\"woodcuttr\":1},\"buildings\":{\"type_nada\":1}}},{\"id\":\"default\"}]}",
                Source);

            Assert.True(result.HasRules, string.Join("; ", result.Errors));
            Assert.Contains(result.Warnings, w => w.Contains("job id 'farmr'") && w.Contains("pathways[0].job_weights"));
            Assert.Contains(result.Warnings, w => w.Contains("building type id 'type_nope'") && w.Contains("pathways[0].building_priority"));
            Assert.Contains(result.Warnings, w => w.Contains("job id 'woodcuttr'") && w.Contains("capacity_multipliers.jobs"));
            Assert.Contains(result.Warnings, w => w.Contains("building type id 'type_nada'") && w.Contains("capacity_multipliers.buildings"));
            Assert.Equal(4, result.Warnings.Count);
        }

        [Fact]
        public void KnownIdsInPathwayMapsDoNotWarn()
        {
            RulesLoadResult result = RulesLoader.Parse(
                "{\"pathways\":[{\"id\":\"a\",\"when\":{\"any\":[\"coastal\"]},\"job_weights\":{\"farmer\":2},\"building_priority\":{\"type_house\":2}},{\"id\":\"default\"}]}",
                Source);

            Assert.True(result.HasRules);
            Assert.Empty(result.Warnings);
        }

        [Fact]
        public void MissingFileAfterFileRulesIsAFailedResultThatKeepsThem()
        {
            RulesLoadResult loaded = RulesLoadResult.Defaults(new[] { "rules file not found" });

            RulesLoadResult applied = RulesReloadPolicy.Apply(loaded, PreviousFile);

            Assert.False(applied.HasRules);
            Assert.Equal(new[] { "rules file missing, kept last good" }, applied.Errors);
        }

        [Theory]
        [InlineData(RulesLoadResult.DefaultsSource)]
        [InlineData(null)]
        public void MissingFileAtStartupStillAdoptsTheDefaults(string currentSource)
        {
            RulesLoadResult loaded = RulesLoadResult.Defaults(new[] { "rules file not found" });

            Assert.Same(loaded, RulesReloadPolicy.Apply(loaded, currentSource));
        }

        [Fact]
        public void AFileThatLoadsOrFailsToParseIsPassedThroughWhateverTheCurrentSource()
        {
            RulesLoadResult loaded = RulesLoader.Parse("{}", Source);
            RulesLoadResult failed = RulesLoader.Parse("{", Source);

            Assert.Same(loaded, RulesReloadPolicy.Apply(loaded, PreviousFile));
            Assert.Same(failed, RulesReloadPolicy.Apply(failed, PreviousFile));
        }

        [Fact]
        public void ARealMissingFileGivesDefaultsFromTheLoaderAndFailsTheReload()
        {
            RulesLoadResult loaded = RulesLoader.Load(PreviousFile + ".does-not-exist");

            Assert.Equal(RulesLoadResult.DefaultsSource, loaded.Source);
            Assert.False(RulesReloadPolicy.Apply(loaded, PreviousFile).HasRules);
        }

        [Theory]
        [InlineData("99")]
        [InlineData("\"forest_rich\",99")]
        [InlineData("-5")]
        [InlineData("0")]
        [InlineData("\"1\"")]
        [InlineData("\"not_a_tag\"")]
        public void ATagThatIsNotAKnownNameFailsTheLoad(string tags)
        {
            RulesLoadResult result = RulesLoader.Parse(PathwayWithAny(tags), Source);

            Assert.False(result.HasRules);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void TagNamesStillLoadAsTheEnum()
        {
            RulesLoadResult result = RulesLoader.Parse(PathwayWithAny("\"forest_rich\",\"coastal\""), Source);

            Assert.True(result.HasRules, string.Join("; ", result.Errors));
            Assert.Equal(new[] { ScenarioTag.ForestRich, ScenarioTag.Coastal }, result.Rules.Pathways[0].When.Any);
        }

        [Fact]
        public void TheDefaultRulesStillSerializeAndRoundTrip()
        {
            RulesLoadResult result = RulesLoader.Parse(RulesJson.Serialize(ColonyRules.Default), Source);

            Assert.True(result.HasRules, string.Join("; ", result.Errors));
            Assert.Empty(result.Warnings);
        }

        [Fact]
        public void ANaNOnAUserKeyTakesTheRangeMinimumNotAnotherFieldsDefault()
        {
            Assert.NotEqual(0.0, ColonyRules.Default.Capacity.Jobs["farmer"].MaxPerZone);

            RulesLoadResult result = RulesLoader.Parse(
                "{\"capacity\":{\"jobs\":{\"['farmer']\":{\"max_per_zone\":NaN}}}}", Source);

            Assert.True(result.HasRules, string.Join("; ", result.Errors));
            Assert.Equal(0.0, result.Rules.Capacity.Jobs["['farmer']"].MaxPerZone);
        }

        [Fact]
        public void ANaNOnAKnownFieldTakesItsShippedDefault()
        {
            RulesLoadResult result = RulesLoader.Parse("{\"capacity\":{\"jobs\":{\"farmer\":{\"max_per_zone\":NaN}}}}", Source);

            Assert.True(result.HasRules);
            Assert.Equal(ColonyRules.Default.Capacity.Jobs["farmer"].MaxPerZone, result.Rules.Capacity.Jobs["farmer"].MaxPerZone);
        }

        [Fact]
        public void WarningsAreCappedWithACountOfTheRest()
        {
            string keys = string.Join(",", Enumerable.Range(0, 120).Select(i => $"\"junk{i}\":1"));

            RulesLoadResult result = RulesLoader.Parse("{" + keys + "}", Source);

            Assert.True(result.HasRules);
            Assert.Equal(RulesLoader.MaxWarnings + 1, result.Warnings.Count);
            Assert.Equal("70 more warnings suppressed", result.Warnings.Last());
        }

        [Fact]
        public void WarningsAtTheCapAreNotTrimmed()
        {
            string keys = string.Join(",", Enumerable.Range(0, RulesLoader.MaxWarnings).Select(i => $"\"junk{i}\":1"));

            RulesLoadResult result = RulesLoader.Parse("{" + keys + "}", Source);

            Assert.Equal(RulesLoader.MaxWarnings, result.Warnings.Count);
            Assert.DoesNotContain(result.Warnings, w => w.Contains("suppressed"));
        }

        [Fact]
        public void TheCapNeverDropsTheThresholdWarning()
        {
            string keys = string.Join(",", Enumerable.Range(0, 120).Select(i => $"\"junk{i}\":1"));

            RulesLoadResult result = RulesLoader.Parse(
                "{" + keys + ",\"scenarios\":{\"rocky\":{\"min_rock_share\":0}}}", Source);

            Assert.True(result.HasRules);
            Assert.Contains(result.Warnings, w => w.Contains("scenarios.rocky.min_rock_share = 0"));
            Assert.Equal("70 more warnings suppressed", result.Warnings.Single(w => w.Contains("suppressed")));
        }

        [Theory]
        [InlineData("forest_rich", "min_trees_per_zone")]
        [InlineData("fertile_plains", "min_farmable_share")]
        [InlineData("rocky", "min_rock_share")]
        [InlineData("scarce", "min_harsh_share")]
        [InlineData("coastal", "min_ocean_share")]
        [InlineData("crowded_frontier", "min_hostile_neighbours")]
        [InlineData("open_frontier", "min_unclaimed_ring_share")]
        public void AZeroMinimumThresholdWarnsThatItTurnsTheTagOnEverywhere(string tag, string field)
        {
            RulesLoadResult result = RulesLoader.Parse(
                "{\"scenarios\":{\"" + tag + "\":{\"" + field + "\":0}}}", Source);

            Assert.True(result.HasRules);
            string warning = Assert.Single(result.Warnings);
            Assert.Contains($"scenarios.{tag}.{field} = 0", warning);
            Assert.Contains("every city", warning);
        }

        [Fact]
        public void MaximumThresholdsOfZeroDoNotWarn()
        {
            RulesLoadResult result = RulesLoader.Parse("{\"scenarios\":{\"scarce\":{\"max_trees_per_zone\":0}}}", Source);

            Assert.True(result.HasRules);
            Assert.Empty(result.Warnings);
        }
    }
}
