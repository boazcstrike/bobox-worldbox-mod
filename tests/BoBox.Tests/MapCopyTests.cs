using System.Collections.Generic;
using BoBox.Planner;
using Xunit;

namespace BoBox.Tests
{
    public class MapCopyTests
    {
        [Fact]
        public void ResultCannotBeCastToDictionary()
        {
            var source = new Dictionary<string, int> { ["a"] = 1 };

            IReadOnlyDictionary<string, int> copy = MapCopy.Of(source);

            Assert.False(copy is Dictionary<string, int>);
        }

        [Fact]
        public void CopyIsUnchangedAfterTheSourceChanges()
        {
            var source = new Dictionary<string, int> { ["a"] = 1 };
            IReadOnlyDictionary<string, int> copy = MapCopy.Of(source);

            source["a"] = 2;
            source["b"] = 3;
            source.Remove("a");

            Assert.Single(copy);
            Assert.Equal(1, copy["a"]);
        }

        [Fact]
        public void NullGivesAnEmptyMap()
        {
            IReadOnlyDictionary<string, int> copy = MapCopy.Of<string, int>(null);

            Assert.NotNull(copy);
            Assert.Empty(copy);
        }
    }
}
