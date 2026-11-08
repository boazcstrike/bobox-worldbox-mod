using System.Collections.Generic;
using System.Linq;

namespace BoBox.Planner.Scenario
{
    /// <summary>Output of <see cref="ScenarioClassifier.Classify"/>. Immutable.</summary>
    public sealed class ScenarioResult
    {
        /// <summary>Tags the city matched, in <see cref="ScenarioTag"/> declaration order.</summary>
        public IReadOnlyList<ScenarioTag> Tags { get; }

        /// <summary>Score per tag, for every tag that was scored (matched or not). Higher is stronger.</summary>
        public IReadOnlyDictionary<ScenarioTag, double> Scores { get; }

        /// <summary>The first pathway in the rules whose condition matched <see cref="Tags"/>. Never null.</summary>
        public Pathway Pathway { get; }

        /// <summary>One line on why this pathway won, for logs and the API.</summary>
        public string Reason { get; }

        public ScenarioResult(
            IEnumerable<ScenarioTag> tags,
            IReadOnlyDictionary<ScenarioTag, double> scores,
            Pathway pathway,
            string reason)
        {
            Tags = (tags ?? Enumerable.Empty<ScenarioTag>()).Distinct().OrderBy(tag => tag).ToList().AsReadOnly();
            Scores = MapCopy.Of(scores);
            Pathway = pathway ?? Pathway.Neutral;
            Reason = reason ?? string.Empty;
        }

        public bool Has(ScenarioTag tag) => Tags.Contains(tag);
    }
}
