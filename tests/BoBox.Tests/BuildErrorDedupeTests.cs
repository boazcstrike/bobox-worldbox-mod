using System.Collections.Generic;
using BoBox.Planner.Build;
using Xunit;

namespace BoBox.Tests
{
    public class BuildErrorDedupeTests
    {
        private const long CityId = 7;
        private const double Window = BuildErrorDedupe.WindowSeconds;

        [Fact]
        public void SameMessageIsLoggedOnceWithinTheWindow()
        {
            var dedupe = new BuildErrorDedupe();

            Assert.True(dedupe.ShouldLog(CityId, "A", 0));
            Assert.False(dedupe.ShouldLog(CityId, "A", 5));
            Assert.False(dedupe.ShouldLog(CityId, "A", Window - 1));
        }

        [Fact]
        public void AlternatingMessagesAreLoggedOnceEachWithinTheWindow()
        {
            var dedupe = new BuildErrorDedupe();

            Assert.True(dedupe.ShouldLog(CityId, "A", 0));
            Assert.True(dedupe.ShouldLog(CityId, "B", 5));
            Assert.False(dedupe.ShouldLog(CityId, "A", 10));
            Assert.False(dedupe.ShouldLog(CityId, "B", 15));
        }

        [Fact]
        public void TheSameMessageLogsAgainOnceTheWindowPassed()
        {
            var dedupe = new BuildErrorDedupe();
            dedupe.ShouldLog(CityId, "A", 0);

            Assert.False(dedupe.ShouldLog(CityId, "A", Window - 1));
            Assert.True(dedupe.ShouldLog(CityId, "A", Window + 1));
            Assert.False(dedupe.ShouldLog(CityId, "A", Window + 2));
        }

        [Fact]
        public void WorldTimeGoingBackwardsLogsAgain()
        {
            var dedupe = new BuildErrorDedupe();
            dedupe.ShouldLog(CityId, "A", 1000);

            Assert.True(dedupe.ShouldLog(CityId, "A", 10));
        }

        [Fact]
        public void CitiesAreIndependent()
        {
            var dedupe = new BuildErrorDedupe();
            dedupe.ShouldLog(CityId, "A", 0);

            Assert.True(dedupe.ShouldLog(CityId + 1, "A", 1));
            Assert.False(dedupe.ShouldLog(CityId, "A", 2));
        }

        [Fact]
        public void ClearAllForgetsEveryCity()
        {
            var dedupe = new BuildErrorDedupe();
            dedupe.ShouldLog(CityId, "A", 0);
            dedupe.ShouldLog(CityId + 1, "A", 0);

            dedupe.Clear();

            Assert.Equal(0, dedupe.CityCount);
            Assert.True(dedupe.ShouldLog(CityId, "A", 1));
            Assert.True(dedupe.ShouldLog(CityId + 1, "A", 1));
        }

        [Fact]
        public void MessagesPerCityAreBoundedByForgettingTheOldest()
        {
            var dedupe = new BuildErrorDedupe();
            for (int i = 0; i < BuildErrorDedupe.MaxMessagesPerCity; i++)
            {
                Assert.True(dedupe.ShouldLog(CityId, "m" + i, i));
            }

            Assert.True(dedupe.ShouldLog(CityId, "overflow", 20));
            Assert.False(dedupe.ShouldLog(CityId, "overflow", 21));
            Assert.False(dedupe.ShouldLog(CityId, "m1", 22));
            Assert.True(dedupe.ShouldLog(CityId, "m0", 23));
        }

        [Fact]
        public void PruneDropsOnlyDeadCities()
        {
            var dedupe = new BuildErrorDedupe();
            dedupe.ShouldLog(CityId, "A", 0);
            dedupe.ShouldLog(CityId + 1, "A", 0);

            dedupe.Prune(new HashSet<long> { CityId });

            Assert.Equal(1, dedupe.CityCount);
            Assert.False(dedupe.ShouldLog(CityId, "A", 1));
            Assert.True(dedupe.ShouldLog(CityId + 1, "A", 1));
        }
    }
}
