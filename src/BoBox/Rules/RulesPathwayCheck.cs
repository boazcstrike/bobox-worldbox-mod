using System.Collections.Generic;
using System.Linq;
using BoBox.Planner.Scenario;
using Newtonsoft.Json.Linq;

namespace BoBox.Rules
{
    /// <summary>
    /// Checks the ordered pathway list as the file wrote it: every entry has a unique non-empty id, and
    /// at most one always-matching pathway closes the list. A list with no closing pathway gets the
    /// built-in one appended. Ids are read from the JSON, because <see cref="Pathway"/> turns a missing
    /// id into "default" and the file's mistake would surface as a bogus duplicate.
    /// </summary>
    internal sealed class RulesPathwayCheck
    {
        public ColonyRules Rules { get; }
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }

        private RulesPathwayCheck(ColonyRules rules, IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
        {
            Rules = rules;
            Errors = errors;
            Warnings = warnings;
        }

        /// <param name="rules">The deserialized rules.</param>
        /// <param name="json">The sanitized JSON they came from; the source of the pathway ids.</param>
        public static RulesPathwayCheck Run(ColonyRules rules, JToken json)
        {
            IReadOnlyList<string> ids = PathwayIds(json);
            List<string> errors = IdErrors(ids);
            errors.AddRange(OrderErrors(rules.Pathways));
            var warnings = new List<string>();
            if (errors.Count > 0 || rules.Pathways.Any(p => p.When.IsAlways()))
            {
                return new RulesPathwayCheck(rules, errors, warnings);
            }

            Pathway closing = ColonyRules.Default.DefaultPathway;
            if (ids.Contains(closing.Id))
            {
                errors.Add($"pathway '{closing.Id}' has a condition and no pathway with an empty 'when' closes the list; give it an empty 'when' or rename it");
                return new RulesPathwayCheck(rules, errors, warnings);
            }

            warnings.Add("no default pathway (empty 'when') in pathways, appended the built-in one");
            return new RulesPathwayCheck(rules.WithPathways(rules.Pathways.Concat(new[] { closing }).ToList()), errors, warnings);
        }

        // One entry per object in the file's pathway array; null when the entry has no id.
        private static IReadOnlyList<string> PathwayIds(JToken json)
        {
            var ids = new List<string>();
            if (!(json?["pathways"] is JArray entries))
            {
                return ids;
            }

            foreach (JObject entry in entries.OfType<JObject>())
            {
                JToken id = entry["id"];
                ids.Add(id == null || id.Type == JTokenType.Null ? null : id.ToString());
            }

            return ids;
        }

        private static List<string> IdErrors(IReadOnlyList<string> ids)
        {
            var errors = new List<string>();
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == null)
                {
                    errors.Add($"pathways[{i}] has no 'id'");
                }
                else if (string.IsNullOrWhiteSpace(ids[i]))
                {
                    errors.Add($"pathways[{i}] has an empty id");
                }
            }

            errors.AddRange(ids
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .GroupBy(id => id)
                .Where(g => g.Count() > 1)
                .Select(g => $"duplicate pathway id '{g.Key}'"));
            return errors;
        }

        private static IEnumerable<string> OrderErrors(IReadOnlyList<Pathway> pathways)
        {
            int[] alwaysAt = Enumerable.Range(0, pathways.Count).Where(i => pathways[i].When.IsAlways()).ToArray();
            if (alwaysAt.Length > 1)
            {
                yield return "more than one pathway has an empty 'when'; only the last pathway may match always";
            }
            else if (alwaysAt.Length == 1 && alwaysAt[0] != pathways.Count - 1)
            {
                yield return $"pathway '{pathways[alwaysAt[0]].Id}' has an empty 'when' but is not last, so the ones after it never apply";
            }
        }
    }
}
