using BoBox.Planner.Scenario;
using Xunit;

namespace BoBox.Tests
{
    public class AllDueFlagTests
    {
        [Fact]
        public void TakeIsFalseUntilSet()
        {
            Assert.False(new AllDueFlag().Take());
        }

        [Fact]
        public void TakeReturnsTrueOnceAndClears()
        {
            var flag = new AllDueFlag();
            flag.Set();
            flag.Set();

            Assert.True(flag.Take());
            Assert.False(flag.Take());
        }

        [Fact]
        public void SetAfterTakeIsKeptForTheNextTake()
        {
            var flag = new AllDueFlag();
            flag.Set();
            flag.Take();
            flag.Set();

            Assert.True(flag.Take());
        }
    }
}
