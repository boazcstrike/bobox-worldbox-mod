using System.Collections.Generic;
using System.Linq;

namespace BoBox.Planner.Build
{
    /// <summary>What a city could build right now, in plain values. Input to <see cref="BuildPlanner.Choose"/>.</summary>
    public sealed class BuildSnapshot
    {
        public static readonly BuildSnapshot Empty = new BuildSnapshot(null);

        /// <summary>Candidates in the template's order. Null entries are dropped.</summary>
        public IReadOnlyList<BuildCandidate> Candidates { get; }

        public BuildSnapshot(IEnumerable<BuildCandidate> candidates)
        {
            Candidates = (candidates ?? Enumerable.Empty<BuildCandidate>())
                .Where(candidate => candidate != null)
                .ToList()
                .AsReadOnly();
        }
    }
}
