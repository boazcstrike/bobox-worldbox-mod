using System.Linq;
using BoBox.Rules;
using Xunit;

namespace BoBox.Tests
{
    public class RulesPathwayTests
    {
        private const string Source = "test.json";

        [Fact]
        public void PathwayWithoutAnIdFailsNamingTheEntry()
        {
            RulesLoadResult result = RulesLoader.Parse(
                "{\"pathways\":[{\"id\":\"a\",\"when\":{\"any\":[\"coastal\"]}},{\"when\":{\"any\":[\"rocky\"]}},{\"id\":\"default\"}]}",
                Source);

            Assert.False(result.HasRules);
            string error = Assert.Single(result.Errors);
            Assert.Contains("pathways[1] has no 'id'", error);
        }

        [Fact]
        public void SeveralPathwaysWithoutIdsAreNotReportedAsDuplicateDefault()
        {
            RulesLoadResult result = RulesLoader.Parse("{\"pathways\":[{\"when\":{\"any\":[\"coastal\"]}},{}]}", Source);

            Assert.False(result.HasRules);
            Assert.DoesNotContain(result.Errors, e => e.Contains("duplicate"));
            Assert.Contains(result.Errors, e => e.Contains("pathways[0] has no 'id'"));
            Assert.Contains(result.Errors, e => e.Contains("pathways[1] has no 'id'"));
        }

        [Fact]
        public void EmptyIdFailsNamingTheEntry()
        {
            RulesLoadResult result = RulesLoader.Parse("{\"pathways\":[{\"id\":\"\"}]}", Source);

            Assert.False(result.HasRules);
            Assert.Contains("pathways[0] has an empty id", Assert.Single(result.Errors));
        }

        [Fact]
        public void DuplicateIdOnTheUsersListFailsBeforeTheBuiltInIsAppended()
        {
            RulesLoadResult result = RulesLoader.Parse(
                "{\"pathways\":[{\"id\":\"a\",\"when\":{\"any\":[\"coastal\"]}},{\"id\":\"a\",\"when\":{\"any\":[\"rocky\"]}}]}",
                Source);

            Assert.False(result.HasRules);
            Assert.Equal(new[] { "duplicate pathway id 'a'" }, result.Errors);
        }

        [Fact]
        public void ConditionalPathwayNamedDefaultWithNoClosingPathwayFailsClearly()
        {
            RulesLoadResult result = RulesLoader.Parse(
                "{\"pathways\":[{\"id\":\"default\",\"when\":{\"any\":[\"coastal\"]}}]}", Source);

            Assert.False(result.HasRules);
            string error = Assert.Single(result.Errors);
            Assert.Contains("'default'", error);
            Assert.DoesNotContain("duplicate", error);
        }

        [Fact]
        public void EmptyPathwayListGetsTheBuiltInClosingPathway()
        {
            RulesLoadResult result = RulesLoader.Parse("{\"pathways\":[]}", Source);

            Assert.True(result.HasRules);
            Assert.Equal(new[] { "default" }, result.Rules.Pathways.Select(p => p.Id));
            Assert.Single(result.Warnings);
        }
    }
}
