using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace BoBox.Rules
{
    /// <summary>
    /// Checks the scenario thresholds as the sanitized JSON has them. A <c>min_*</c> threshold of 0 is
    /// met by every city, so writing 0 to switch a tag off switches it on for all of them.
    /// </summary>
    internal static class RulesThresholdCheck
    {
        private const string MinimumPrefix = "min_";

        public static IEnumerable<string> ZeroMinimumWarnings(JToken json)
        {
            if (!(json?["scenarios"] is JObject scenarios))
            {
                return Enumerable.Empty<string>();
            }

            return scenarios.Properties()
                .Where(tag => tag.Value is JObject)
                .SelectMany(tag => ((JObject)tag.Value).Properties()
                    .Where(IsZeroMinimum)
                    .Select(threshold =>
                        $"scenarios.{tag.Name}.{threshold.Name} = 0 turns {tag.Name} on for every city; use a higher value, or a very large one to keep it off"))
                .ToList();
        }

        private static bool IsZeroMinimum(JProperty threshold)
        {
            bool isNumber = threshold.Value.Type == JTokenType.Integer || threshold.Value.Type == JTokenType.Float;
            return threshold.Name.StartsWith(MinimumPrefix, StringComparison.Ordinal) && isNumber && (double)threshold.Value == 0;
        }
    }
}
