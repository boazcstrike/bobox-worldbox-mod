using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BoBox.Rules;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BoBox.Tests
{
    public class RulesLoaderTests
    {
        private const string Source = "test.json";

        private static string Dump(ColonyRules rules) => RulesJson.Serialize(rules);

        private static string RepoRulesPath()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "rules", "bobox-rules.json")))
            {
                dir = Path.GetDirectoryName(dir);
            }

            Assert.NotNull(dir);
            return Path.Combine(dir, "rules", "bobox-rules.json");
        }

        [Fact]
        public void MissingFileGivesTheDefaults()
        {
            RulesLoadResult result = RulesLoader.Load("does-not-exist/bobox-rules.json");

            Assert.True(result.HasRules);
            Assert.Same(ColonyRules.Default, result.Rules);
            Assert.Equal(RulesLoadResult.DefaultsSource, result.Source);
            Assert.Empty(result.Errors);
            Assert.Single(result.Warnings);
        }

        [Fact]
        public void EmptyObjectEqualsTheDefaults()
        {
            RulesLoadResult result = RulesLoader.Parse("{}", Source);

            Assert.True(result.HasRules);
            Assert.Equal(Source, result.Source);
            Assert.Empty(result.Errors);
            Assert.Empty(result.Warnings);
            Assert.Equal(Dump(ColonyRules.Default), Dump(result.Rules));
        }

        [Fact]
        public void PartialOverrideKeepsTheOtherDefaults()
        {
            RulesLoadResult result = RulesLoader.Parse("{\"signals\":{\"cities_per_frame\":5}}", Source);

            ColonyRules defaults = ColonyRules.Default;
            Assert.Empty(result.Errors);
            Assert.Empty(result.Warnings);
            Assert.Equal(5, result.Rules.Signals.CitiesPerFrame);
            Assert.Equal(defaults.Signals.RefreshSeconds, result.Rules.Signals.RefreshSeconds);
            Assert.Equal(defaults.Signals.CoastMinWaterTiles, result.Rules.Signals.CoastMinWaterTiles);
            Assert.Equal(defaults.Version, result.Rules.Version);
            Assert.Equal(defaults.Pathways.Select(p => p.Id), result.Rules.Pathways.Select(p => p.Id));
            Assert.Equal(defaults.Capacity.Jobs.Count, result.Rules.Capacity.Jobs.Count);
            Assert.Equal(defaults.MapScale.Count, result.Rules.MapScale.Count);
            Assert.Equal(defaults.Scenarios.Rocky.MinRockShare, result.Rules.Scenarios.Rocky.MinRockShare);
        }

        [Fact]
        public void PartialCapacityEntryKeepsItsOtherFields()
        {
            RulesLoadResult result = RulesLoader.Parse(
                "{\"capacity\":{\"jobs\":{\"farmer\":{\"max_per_zone\":3}},\"buildings\":{\"type_new\":{\"max_per_zone\":1}}}}",
                Source);

            Assert.Empty(result.Errors);
            CapacityBase farmer = result.Rules.Capacity.Jobs.Values.Single(c => c.MaxPerZone == 3);
            Assert.True(farmer.MaxCeiling > 0);
            CapacityBase added = result.Rules.Capacity.Buildings["type_new"];
            Assert.Equal(CapacityBase.NoCeiling, added.MaxCeiling);
        }

        [Fact]
        public void InvalidJsonFailsWithLineAndColumn()
        {
            RulesLoadResult result = RulesLoader.Parse("{\n  \"signals\": {\n    \"cities_per_frame\": ,\n  }\n}", Source);

            Assert.False(result.HasRules);
            Assert.Null(result.Rules);
            Assert.Equal(Source, result.Source);
            string error = Assert.Single(result.Errors);
            Assert.Contains("line 3", error);
            Assert.Contains("column", error);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("[]")]
        [InlineData("42")]
        [InlineData("{\"signals\":{},\"signals\":{}}")]
        [InlineData("{\"signals\":{\"cities_per_frame\":\"many\"}}")]
        public void UnusableContentFailsAndKeepsNoRules(string json)
        {
            RulesLoadResult result = RulesLoader.Parse(json, Source);

            Assert.False(result.HasRules);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void UnknownKeysAreIgnoredWithAWarningNamingThePath()
        {
            RulesLoadResult result = RulesLoader.Parse(
                "{\"colour\":1,\"signals\":{\"cities_per_frame\":4,\"warp\":true},\"pathways\":[{\"id\":\"x\",\"oops\":1}]}",
                Source);

            Assert.True(result.HasRules);
            Assert.Empty(result.Errors);
            Assert.Equal(4, result.Rules.Signals.CitiesPerFrame);
            Assert.Contains(result.Warnings, w => w.Contains("'colour'"));
            Assert.Contains(result.Warnings, w => w.Contains("'signals.warp'"));
            Assert.Contains(result.Warnings, w => w.Contains("'pathways[0].oops'"));
        }

        [Fact]
        public void OutOfRangeNumbersAreClampedPerFieldWithWarnings()
        {
            RulesLoadResult result = RulesLoader.Parse(
                "{\"signals\":{\"cities_per_frame\":500,\"refresh_seconds\":0.1,\"free_zone_scan_radius\":-3}," +
                "\"scenarios\":{\"rocky\":{\"min_rock_share\":1.5,\"min_minerals_per_zone\":2.5}}}",
                Source);

            Assert.True(result.HasRules);
            Assert.Empty(result.Errors);
            Assert.Equal(SignalBounds.MaxCitiesPerFrame, result.Rules.Signals.CitiesPerFrame);
            Assert.Equal(SignalBounds.MinRefreshSeconds, result.Rules.Signals.RefreshSeconds);
            Assert.Equal(0, result.Rules.Signals.FreeZoneScanRadius);
            Assert.Equal(1.0, result.Rules.Scenarios.Rocky.MinRockShare);
            Assert.Equal(2.5, result.Rules.Scenarios.Rocky.MinMineralsPerZone);
            Assert.Equal(5, result.Warnings.Count);
            Assert.Contains(result.Warnings, w => w.Contains("open_frontier can never match"));
            Assert.Contains(result.Warnings, w => w.Contains("signals.cities_per_frame") && w.Contains("clamped to 10"));
        }

        [Fact]
        public void NegativeCapacityAndWeightValuesAreClamped()
        {
            RulesLoadResult result = RulesLoader.Parse(
                "{\"capacity\":{\"buildings\":{\"type_house\":{\"min_floor\":-2}}}," +
                "\"map_scale\":[{\"min_size\":0,\"factor\":0}]}",
                Source);

            Assert.Empty(result.Errors);
            Assert.Equal(0, result.Rules.Capacity.Buildings["type_house"].MinFloor);
            Assert.Equal(0.1, result.Rules.MapScale.Single().Factor);
            Assert.Equal(2, result.Warnings.Count);
        }

        [Fact]
        public void DuplicatePathwayIdFails()
        {
            RulesLoadResult result = RulesLoader.Parse(
                "{\"pathways\":[{\"id\":\"a\",\"when\":{\"any\":[\"coastal\"]}},{\"id\":\"a\",\"when\":{\"any\":[\"rocky\"]}}," +
                "{\"id\":\"default\"}]}",
                Source);

            Assert.False(result.HasRules);
            Assert.Contains(result.Errors, e => e.Contains("duplicate pathway id 'a'"));
        }

        [Fact]
        public void MissingDefaultPathwayIsAppendedWithAWarning()
        {
            RulesLoadResult result = RulesLoader.Parse(
                "{\"pathways\":[{\"id\":\"only\",\"when\":{\"any\":[\"coastal\"]}}]}", Source);

            Assert.True(result.HasRules);
            Assert.Equal(new[] { "only", "default" }, result.Rules.Pathways.Select(p => p.Id));
            Assert.True(result.Rules.Pathways.Last().When.IsAlways());
            Assert.Single(result.Warnings);
        }

        [Fact]
        public void ADefaultPathwayThatIsNotLastFails()
        {
            RulesLoadResult result = RulesLoader.Parse(
                "{\"pathways\":[{\"id\":\"all\"},{\"id\":\"late\",\"when\":{\"any\":[\"coastal\"]}}]}", Source);

            Assert.False(result.HasRules);
            Assert.Contains(result.Errors, e => e.Contains("'all'") && e.Contains("not last"));
        }

        [Fact]
        public void AnOlderFileVersionWarns()
        {
            RulesLoadResult result = RulesLoader.Parse("{\"version\":0}", Source);

            Assert.True(result.HasRules);
            Assert.Single(result.Warnings);
            Assert.Contains("version 0", result.Warnings[0]);
        }

        [Fact]
        public void ANewerFileVersionWarnsThatUnknownKeysAreIgnored()
        {
            RulesLoadResult result = RulesLoader.Parse("{\"version\":99,\"future\":1}", Source);

            Assert.True(result.HasRules);
            Assert.Contains(result.Warnings, w => w.Contains("version 99") && w.Contains("newer than this mod"));
            Assert.Contains(result.Warnings, w => w.Contains("'future'"));
        }

        [Fact]
        public void AnOversizedFileFailsWithoutBeingParsed()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, new string(' ', RulesLoader.MaxFileBytes + 1));

                RulesLoadResult result = RulesLoader.Load(path);

                Assert.False(result.HasRules);
                Assert.Contains("byte limit", Assert.Single(result.Errors));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void UnknownCapacityIdsWarnButStillLoad()
        {
            RulesLoadResult result = RulesLoader.Parse(
                "{\"capacity\":{\"jobs\":{\"farmr\":{\"max_per_zone\":1}},\"buildings\":{\"type_nope\":{\"max_per_zone\":1}}}}",
                Source);

            Assert.True(result.HasRules);
            Assert.Empty(result.Errors);
            Assert.Equal(1, result.Rules.Capacity.Jobs["farmr"].MaxPerZone);
            Assert.Equal(1, result.Rules.Capacity.Buildings["type_nope"].MaxPerZone);
            Assert.Contains(result.Warnings, w => w.Contains("job id 'farmr'") && w.Contains("capacity.jobs"));
            Assert.Contains(result.Warnings, w => w.Contains("building type id 'type_nope'") && w.Contains("capacity.buildings"));
            Assert.Equal(2, result.Warnings.Count);
        }

        [Fact]
        public void CapacityMapsCannotBeChangedThroughTheInterface()
        {
            var jobs = (IDictionary<string, CapacityBase>)ColonyRules.Default.Capacity.Jobs;

            Assert.True(jobs.IsReadOnly);
            Assert.True(((IDictionary<string, CapacityBase>)new CapacityRules().Jobs).IsReadOnly);
        }

        [Fact]
        public void SerializeThenParseRoundTrips()
        {
            string json = RulesJson.Serialize(ColonyRules.Default);

            RulesLoadResult result = RulesLoader.Parse(json, Source);

            Assert.True(result.HasRules);
            Assert.Empty(result.Errors);
            Assert.Empty(result.Warnings);
            Assert.Equal(json, Dump(result.Rules));
        }

        [Fact]
        public void CommittedRulesFileParsesCleanAndMirrorsTheDefaults()
        {
            string path = RepoRulesPath();
            JToken onDisk = JToken.Parse(File.ReadAllText(path));

            // The merged result hides a key the file omits, so compare the raw file with the defaults too.
            Assert.True(
                JToken.DeepEquals(onDisk, RulesJson.ToJToken(ColonyRules.Default)),
                "rules/bobox-rules.json differs from ColonyRules.Default (a key is missing, extra, or has another value)");

            RulesLoadResult result = RulesLoader.Load(path);

            Assert.True(result.HasRules);
            Assert.Empty(result.Errors);
            Assert.Empty(result.Warnings);
            Assert.Equal(Dump(ColonyRules.Default), Dump(result.Rules));
        }
    }
}
