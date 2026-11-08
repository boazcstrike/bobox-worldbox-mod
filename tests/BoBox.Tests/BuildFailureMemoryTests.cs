using System.Collections.Generic;
using BoBox.Planner.Build;
using Xunit;

namespace BoBox.Tests
{
    public class BuildFailureMemoryTests
    {
        private const long CityId = 7;

        private static BuildCandidate Building(string type = "type_house") => new BuildCandidate("order_" + type, type);

        private static BuildCandidate Upgrade(string type = "type_house") =>
            new BuildCandidate("upgrade_" + type, type, isUpgrade: true);

        [Fact]
        public void FailureBlocksTheTypeUntilTheCooldownEnds()
        {
            var memory = new BuildFailureMemory();
            memory.Remember(CityId, new[] { Building() }, now: 100);

            Assert.True(memory.HasFailedRecently(CityId, Building(), 100));
            Assert.True(memory.HasFailedRecently(CityId, Building(), 100 + BuildFailureMemory.CooldownSeconds - 0.1));
            Assert.False(memory.HasFailedRecently(CityId, Building(), 100 + BuildFailureMemory.CooldownSeconds));
        }

        [Fact]
        public void NowBeforeTheFailureCountsAsExpired()
        {
            var memory = new BuildFailureMemory();
            memory.Remember(CityId, new[] { Building() }, now: 100);

            Assert.False(memory.HasFailedRecently(CityId, Building(), 99));
        }

        [Fact]
        public void FailedUpgradeDoesNotBlockNewBuildingOfTheSameType()
        {
            var memory = new BuildFailureMemory();
            memory.Remember(CityId, new[] { Upgrade() }, now: 10);

            Assert.True(memory.HasFailedRecently(CityId, Upgrade(), 10));
            Assert.False(memory.HasFailedRecently(CityId, Building(), 10));
        }

        [Fact]
        public void FailedUpgradeOrderDoesNotBlockAnotherUpgradeOrderOfTheSameType()
        {
            var memory = new BuildFailureMemory();
            var first = new BuildCandidate("house_upgrade_1", "type_house", isUpgrade: true);
            var second = new BuildCandidate("house_upgrade_2", "type_house", isUpgrade: true);
            memory.Remember(CityId, new[] { first }, now: 10);

            Assert.True(memory.HasFailedRecently(CityId, first, 10));
            Assert.False(memory.HasFailedRecently(CityId, second, 10));
        }

        [Fact]
        public void FailureIsPerCity()
        {
            var memory = new BuildFailureMemory();
            memory.Remember(CityId, new[] { Building() }, now: 10);

            Assert.False(memory.HasFailedRecently(CityId + 1, Building(), 10));
        }

        [Fact]
        public void PruneDropsCitiesThatAreNotLive()
        {
            var memory = new BuildFailureMemory();
            memory.Remember(1, new[] { Building() }, now: 10);
            memory.Remember(2, new[] { Building() }, now: 10);

            memory.Prune(new HashSet<long> { 2 });

            Assert.Equal(1, memory.CityCount);
            Assert.False(memory.HasFailedRecently(1, Building(), 10));
            Assert.True(memory.HasFailedRecently(2, Building(), 10));
        }

        [Fact]
        public void NewFailureDropsExpiredEntries()
        {
            var memory = new BuildFailureMemory();
            memory.Remember(CityId, new[] { Building("type_a") }, now: 0);
            memory.Remember(CityId, new[] { Building("type_b") }, now: 100);

            Assert.Equal(1, memory.EntryCount(CityId));
            Assert.True(memory.HasFailedRecently(CityId, Building("type_b"), 100));
        }

        [Fact]
        public void RepeatedFailureOfOneTypeKeepsOneEntry()
        {
            var memory = new BuildFailureMemory();
            for (int i = 0; i < 100; i++)
            {
                memory.Remember(CityId, new[] { Building() }, now: i);
            }

            Assert.Equal(1, memory.EntryCount(CityId));
        }

        [Fact]
        public void EntriesPerCityAreBoundedAndTheOldestGoFirst()
        {
            var memory = new BuildFailureMemory();
            int total = BuildFailureMemory.MaxEntriesPerCity + 10;
            for (int i = 0; i < total; i++)
            {
                memory.Remember(CityId, new[] { Building("type_" + i) }, now: i * 0.1);
            }

            double now = (total - 1) * 0.1;
            Assert.Equal(BuildFailureMemory.MaxEntriesPerCity, memory.EntryCount(CityId));
            Assert.True(memory.HasFailedRecently(CityId, Building("type_" + (total - 1)), now));
            Assert.False(memory.HasFailedRecently(CityId, Building("type_0"), now));
        }

        [Fact]
        public void EmptyFailureListChangesNothing()
        {
            var memory = new BuildFailureMemory();
            memory.Remember(CityId, new BuildCandidate[0], now: 10);

            Assert.Equal(0, memory.CityCount);
        }

        [Fact]
        public void ClearForgetsEveryCityAndItsCooldowns()
        {
            var memory = new BuildFailureMemory();
            memory.Remember(CityId, new[] { Building(), Upgrade() }, now: 10);
            memory.Remember(CityId + 1, new[] { Building() }, now: 10);

            memory.Clear();

            Assert.Equal(0, memory.CityCount);
            Assert.Equal(0, memory.EntryCount(CityId));
            Assert.False(memory.HasFailedRecently(CityId, Building(), 10));
            Assert.False(memory.HasFailedRecently(CityId, Upgrade(), 10));
        }

        [Fact]
        public void KeyFromOrderPartsMatchesTheCandidateKey()
        {
            var memory = new BuildFailureMemory();
            memory.Remember(CityId, new[] { Building(), Upgrade() }, now: 10);

            Assert.True(memory.HasFailedRecently(CityId, BuildFailureMemory.KeyOf(false, "order_type_house", "type_house"), 10));
            Assert.True(memory.HasFailedRecently(CityId, BuildFailureMemory.KeyOf(true, "upgrade_type_house", "type_house"), 10));
            Assert.False(memory.HasFailedRecently(CityId, BuildFailureMemory.KeyOf(true, "other_upgrade", "type_house"), 10));
        }
    }
}
