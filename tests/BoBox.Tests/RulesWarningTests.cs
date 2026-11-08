using System.Linq;
using BoBox.Rules;
using Xunit;

namespace BoBox.Tests
{
    public class RulesWarningTests
    {
        private const string Source = "test.json";

        [Fact]
        public void NullInACapacityMapDeletesTheDefaultEntryWithAWarning()
        {
            RulesLoadResult result = RulesLoader.Parse(
                "{\"capacity\":{\"jobs\":{\"farmer\":null},\"buildings\":{\"type_house\":null}}}", Source);

            Assert.True(result.HasRules);
            Assert.Empty(result.Errors);
            Assert.False(result.Rules.Capacity.Jobs.ContainsKey("farmer"));
            Assert.False(result.Rules.Capacity.Buildings.ContainsKey("type_house"));
            Assert.Equal(ColonyRules.Default.Capacity.Jobs.Count - 1, result.Rules.Capacity.Jobs.Count);
            Assert.Contains(result.Warnings, w => w.Contains("removed default capacity 'farmer'"));
            Assert.Contains(result.Warnings, w => w.Contains("removed default capacity 'type_house'"));
            Assert.Equal(2, result.Warnings.Count);
        }

        [Fact]
        public void NullForAnIdTheDefaultsLackAddsNothing()
        {
            RulesLoadResult result = RulesLoader.Parse("{\"capacity\":{\"jobs\":{\"nobody\":null}}}", Source);

            Assert.True(result.HasRules);
            Assert.False(result.Rules.Capacity.Jobs.ContainsKey("nobody"));
            Assert.Equal(ColonyRules.Default.Capacity.Jobs.Count, result.Rules.Capacity.Jobs.Count);
            Assert.Empty(result.Warnings);
        }

        [Fact]
        public void NullOutsideACapacityMapStillKeepsTheDefault()
        {
            RulesLoadResult result = RulesLoader.Parse(
                "{\"signals\":{\"refresh_seconds\":null},\"capacity\":{\"jobs\":{\"farmer\":{\"max_per_zone\":null}}}}", Source);

            Assert.True(result.HasRules);
            Assert.Empty(result.Warnings);
            Assert.Equal(ColonyRules.Default.Signals.RefreshSeconds, result.Rules.Signals.RefreshSeconds);
            Assert.Equal(ColonyRules.Default.Capacity.Jobs["farmer"].MaxPerZone, result.Rules.Capacity.Jobs["farmer"].MaxPerZone);
        }

        [Fact]
        public void AnOlderVersionWarningSaysTheArraysAreUsedAsIs()
        {
            RulesLoadResult result = RulesLoader.Parse("{\"version\":0}", Source);

            string warning = Assert.Single(result.Warnings);
            Assert.Contains("pathways", warning);
            Assert.Contains("map_scale", warning);
            Assert.Contains("used as is", warning);
        }

        [Fact]
        public void MoreFreeZonesWantedThanTheScanCoversWarns()
        {
            RulesLoadResult result = RulesLoader.Parse(
                "{\"signals\":{\"free_zone_scan_radius\":1},\"scenarios\":{\"open_frontier\":{\"min_free_zones_in_range\":10}}}",
                Source);

            Assert.True(result.HasRules);
            string warning = Assert.Single(result.Warnings);
            Assert.Contains("min_free_zones_in_range = 10", warning);
            Assert.Contains("9 zones", warning);
        }

        [Fact]
        public void FreeZonesWantedAtTheScanSizeDoNotWarn()
        {
            RulesLoadResult result = RulesLoader.Parse(
                "{\"signals\":{\"free_zone_scan_radius\":1},\"scenarios\":{\"open_frontier\":{\"min_free_zones_in_range\":9}}}",
                Source);

            Assert.Empty(result.Warnings);
        }

        [Fact]
        public void NaNTakesTheFieldDefaultNotTheRangeMinimum()
        {
            RulesLoadResult result = RulesLoader.Parse(
                "{\"signals\":{\"refresh_seconds\":NaN,\"cities_per_frame\":NaN},\"capacity\":{\"jobs\":{\"farmer\":{\"max_per_zone\":NaN}}}}",
                Source);

            ColonyRules defaults = ColonyRules.Default;
            Assert.True(result.HasRules, string.Join("; ", result.Errors));
            Assert.Equal(defaults.Signals.RefreshSeconds, result.Rules.Signals.RefreshSeconds);
            Assert.Equal(defaults.Signals.CitiesPerFrame, result.Rules.Signals.CitiesPerFrame);
            Assert.Equal(defaults.Capacity.Jobs["farmer"].MaxPerZone, result.Rules.Capacity.Jobs["farmer"].MaxPerZone);
            Assert.Equal(3, result.Warnings.Count(w => w.Contains("clamped")));
        }

        [Fact]
        public void SignalValuesOutsideTheSharedBoundsWarn()
        {
            RulesLoadResult result = RulesLoader.Parse(
                "{\"signals\":{\"refresh_seconds\":60,\"coast_cache_seconds\":1,\"free_zone_scan_radius\":50,\"cities_per_frame\":0,\"coast_min_water_tiles\":0}}",
                Source);

            Assert.Equal(SignalBounds.MaxRefreshSeconds, result.Rules.Signals.RefreshSeconds);
            Assert.Equal(SignalBounds.MinCoastCacheSeconds, result.Rules.Signals.CoastCacheSeconds);
            Assert.Equal(SignalBounds.MaxFreeZoneScanRadius, result.Rules.Signals.FreeZoneScanRadius);
            Assert.Equal(SignalBounds.MinCitiesPerFrame, result.Rules.Signals.CitiesPerFrame);
            Assert.Equal(SignalBounds.MinCoastMinWaterTiles, result.Rules.Signals.CoastMinWaterTiles);
            Assert.Equal(5, result.Warnings.Count(w => w.Contains("clamped")));
        }

        [Fact]
        public void MinOceanShareDefaultsToATenthAndIsClampedToAShare()
        {
            Assert.Equal(0.1, ColonyRules.Default.Scenarios.Coastal.MinOceanShare);

            RulesLoadResult result = RulesLoader.Parse("{\"scenarios\":{\"coastal\":{\"min_ocean_share\":2}}}", Source);

            Assert.Equal(1.0, result.Rules.Scenarios.Coastal.MinOceanShare);
            Assert.Contains(result.Warnings, w => w.Contains("scenarios.coastal.min_ocean_share") && w.Contains("clamped to 1"));
        }
    }
}
