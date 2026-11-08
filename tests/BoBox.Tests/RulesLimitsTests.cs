using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BoBox.Rules;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BoBox.Tests
{
    public class RulesLimitsTests
    {
        private const string Source = "test.json";
        private const string AnyKey = "x";
        private const string AnyPathwayId = "p";
        private const string HugeValue = "1000000000000";
        private static readonly HashSet<string> UnlimitedPaths = new HashSet<string> { "version" };

        public static IEnumerable<object[]> LimitedFields() => RulesLimits.Keys.Select(key => new object[] { key });

        // "a.b[].c.*" becomes {"a":{"b":[{"c":{"x": value}}]}}; a pathway entry gets an id so only the clamp is under test.
        private static JToken Build(string[] names, int index, JToken leaf)
        {
            if (index == names.Length)
            {
                return leaf;
            }

            string name = names[index];
            JToken rest = Build(names, index + 1, leaf);
            if (name == "*")
            {
                return new JObject { [AnyKey] = rest };
            }

            if (!name.EndsWith("[]", StringComparison.Ordinal))
            {
                return new JObject { [name] = rest };
            }

            string key = name.Substring(0, name.Length - 2);
            var entry = (JObject)rest;
            if (key == "pathways")
            {
                entry["id"] = AnyPathwayId;
            }

            return new JObject { [key] = new JArray(entry) };
        }

        private static void AssertClamped(string pattern, string value, double expected)
        {
            string json = Build(pattern.Split('.'), 0, JToken.Parse(value)).ToString();

            RulesLoadResult result = RulesLoader.Parse(json, Source);

            Assert.True(result.HasRules, string.Join("; ", result.Errors));
            string warning = Assert.Single(result.Warnings, w => w.Contains("clamped"));
            Assert.Contains(FormattableString.Invariant($"clamped to {expected}"), warning);
        }

        [Theory]
        [MemberData(nameof(LimitedFields))]
        public void EveryLimitedFieldIsClampedFromBothSides(string pattern)
        {
            Assert.True(RulesLimits.TryGet(pattern, out RulesLimits.Range range));

            AssertClamped(pattern, HugeValue, range.Max);
            AssertClamped(pattern, "-" + HugeValue, range.Min);
        }

        [Fact]
        public void SignalLimitsAreTheSharedSignalBounds()
        {
            AssertLimit("signals.cities_per_frame", SignalBounds.MinCitiesPerFrame, SignalBounds.MaxCitiesPerFrame);
            AssertLimit("signals.refresh_seconds", SignalBounds.MinRefreshSeconds, SignalBounds.MaxRefreshSeconds);
            AssertLimit("signals.coast_cache_seconds", SignalBounds.MinCoastCacheSeconds, SignalBounds.MaxCoastCacheSeconds);
            AssertLimit("signals.coast_min_water_tiles", SignalBounds.MinCoastMinWaterTiles, SignalBounds.MaxCoastMinWaterTiles);
            AssertLimit("signals.free_zone_scan_radius", SignalBounds.MinFreeZoneScanRadius, SignalBounds.MaxFreeZoneScanRadius);
        }

        private static void AssertLimit(string pattern, double min, double max)
        {
            Assert.True(RulesLimits.TryGet(pattern, out RulesLimits.Range range));
            Assert.Equal(min, range.Min);
            Assert.Equal(max, range.Max);
        }

        [Fact]
        public void EveryNumericDefaultHasALimitThatContainsIt()
        {
            var problems = new List<string>();
            IEnumerable<JValue> leaves = ((JObject)RulesJson.ToJToken(ColonyRules.Default)).DescendantsAndSelf().OfType<JValue>();
            foreach (JValue leaf in leaves.Where(IsLimitedNumber))
            {
                string pattern = RulesLimits.Keys.FirstOrDefault(key => Matches(key, leaf.Path));
                if (pattern == null)
                {
                    problems.Add($"{leaf.Path}: no entry in RulesLimits");
                    continue;
                }

                RulesLimits.TryGet(pattern, out RulesLimits.Range range);
                double value = (double)leaf;
                if (value < range.Min || value > range.Max)
                {
                    problems.Add(FormattableString.Invariant($"{leaf.Path} = {value} is outside {pattern} {range.Min}..{range.Max}"));
                }
            }

            Assert.True(problems.Count == 0, string.Join("; ", problems));
        }

        private static bool IsLimitedNumber(JValue leaf)
        {
            bool isNumber = leaf.Type == JTokenType.Integer || leaf.Type == JTokenType.Float;
            return isNumber && !UnlimitedPaths.Contains(leaf.Path);
        }

        // An array index matches "[]", any map key matches "*"; every other segment must be equal.
        private static bool Matches(string pattern, string path)
        {
            string[] want = pattern.Split('.');
            string[] have = Regex.Replace(path, @"\[\d+\]", "[]").Split('.');
            return want.Length == have.Length && want.Zip(have, (w, h) => w == "*" || w == h).All(same => same);
        }

        [Fact]
        public void BuildingPriorityIsLimitedSoPlannerArithmeticCannotOverflow()
        {
            Assert.True(RulesLimits.TryGet("pathways[].building_priority.*", out RulesLimits.Range range));
            Assert.True(range.Max < int.MaxValue);
            Assert.True(range.Min >= 0);
        }

        [Fact]
        public void MapScaleEntryWithoutFactorKeepsTheNeutralFactor()
        {
            RulesLoadResult result = RulesLoader.Parse("{\"map_scale\":[{\"min_size\":4}]}", Source);

            Assert.Empty(result.Errors);
            Assert.Equal(1.0, result.Rules.MapScale.Single().Factor);
            Assert.Equal(1.0, result.Rules.ScaleForMapSize(4));
        }

        [Fact]
        public void PathwayEntryWithOnlyAnIdHasEmptyMaps()
        {
            RulesLoadResult result = RulesLoader.Parse("{\"pathways\":[{\"id\":\"x\"}]}", Source);

            Assert.Empty(result.Errors);
            BoBox.Planner.Scenario.Pathway pathway = result.Rules.Pathways.Single();
            Assert.Equal("x", pathway.Id);
            Assert.Empty(pathway.JobWeights);
            Assert.Empty(pathway.BuildingPriority);
            Assert.Empty(pathway.CapacityMultipliers.Jobs);
            Assert.Empty(pathway.CapacityMultipliers.Buildings);
        }
    }
}
