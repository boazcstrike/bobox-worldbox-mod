using System;
using System.Collections.Generic;
using System.Linq;
using BoBox.Planner;
using BoBox.Planner.Build;

namespace BoBox.Rules
{
    /// <summary>
    /// The citizen job ids (<see cref="JobIds"/>) and building type ids (<see cref="BuildingTypes"/>)
    /// the planner knows. An entry under another id still loads; the sanitizer warns, because a
    /// misspelt id is the likeliest reason a rule has no effect. Covers the capacity maps and the
    /// per-pathway job weights, building priorities and capacity multipliers.
    /// </summary>
    internal static class RulesKnownIds
    {
        private static readonly HashSet<string> Jobs = Constants(typeof(JobIds));
        private static readonly HashSet<string> Buildings = Constants(typeof(BuildingTypes));

        private static readonly IReadOnlyDictionary<string, string> JobPatterns = new Dictionary<string, string>
        {
            ["capacity.jobs"] = "job",
            ["pathways[].job_weights"] = "job",
            ["pathways[].capacity_multipliers.jobs"] = "job",
        };

        private static readonly IReadOnlyDictionary<string, string> BuildingPatterns = new Dictionary<string, string>
        {
            ["capacity.buildings"] = "building type",
            ["pathways[].building_priority"] = "building type",
            ["pathways[].capacity_multipliers.buildings"] = "building type",
        };

        /// <summary>
        /// True when <paramref name="mapPattern"/> is a map keyed by job or building id and
        /// <paramref name="id"/> is not one the planner knows. <paramref name="kind"/> names the map.
        /// </summary>
        public static bool IsUnknown(string mapPattern, string id, out string kind)
        {
            if (JobPatterns.TryGetValue(mapPattern, out kind))
            {
                return !Jobs.Contains(id);
            }

            if (BuildingPatterns.TryGetValue(mapPattern, out kind))
            {
                return !Buildings.Contains(id);
            }

            return false;
        }

        private static HashSet<string> Constants(Type holder)
        {
            return new HashSet<string>(holder
                .GetFields()
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue()));
        }
    }
}
