using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BoBox.Rules;

namespace BoBox.Planner.Scenario
{
    /// <summary>
    /// Scores the 7 scenario tags from a city's signals and picks the first matching pathway from
    /// <see cref="ColonyRules.Pathways"/>. Pure and deterministic. Design: docs/colony-brain.md
    /// (decision tree). Scoring formula and threshold boundaries: <see cref="ScenarioScoring"/>.
    /// </summary>
    public static class ScenarioClassifier
    {
        private static readonly ScenarioTag[] AllTags = (ScenarioTag[])Enum.GetValues(typeof(ScenarioTag));
        private static readonly string[] TagNames = BuildTagNames();

        /// <summary>
        /// Every tag with a score above 0 is included, in declaration order. The pathway is the first
        /// one in <c>rules.Pathways</c> whose condition matches those tags, else the rules' default.
        /// Null rules mean <see cref="ColonyRules.Default"/>; null or empty signals mean no tags.
        /// </summary>
        public static ScenarioResult Classify(ScenarioSignals signals, ColonyRules rules)
        {
            ColonyRules active = rules ?? ColonyRules.Default;
            ScenarioSignals input = signals ?? ScenarioSignals.Empty;

            var tags = new List<ScenarioTag>(AllTags.Length);
            var scores = new Dictionary<ScenarioTag, double>(AllTags.Length);
            for (int i = 0; i < AllTags.Length; i++)
            {
                double score = ScenarioScoring.Score(AllTags[i], input, active.Scenarios);
                scores[AllTags[i]] = score;
                if (score > 0)
                {
                    tags.Add(AllTags[i]);
                }
            }

            Pathway matched = FirstMatch(tags, active.Pathways);
            Pathway chosen = matched ?? active.DefaultPathway;
            return new ScenarioResult(tags, scores, chosen, Explain(tags, scores, chosen, matched != null));
        }

        private static Pathway FirstMatch(List<ScenarioTag> tags, IReadOnlyList<Pathway> pathways)
        {
            for (int i = 0; i < pathways.Count; i++)
            {
                if (pathways[i].When.Matches(tags))
                {
                    return pathways[i];
                }
            }

            return null;
        }

        private static string Explain(
            List<ScenarioTag> tags, Dictionary<ScenarioTag, double> scores, Pathway chosen, bool isMatched)
        {
            var text = new StringBuilder("tags: ");
            if (tags.Count == 0)
            {
                text.Append("none");
            }

            for (int i = 0; i < tags.Count; i++)
            {
                text.Append(i == 0 ? string.Empty : ", ")
                    .Append(NameOf(tags[i]))
                    .Append('=')
                    .Append(scores[tags[i]].ToString("0.00", CultureInfo.InvariantCulture));
            }

            text.Append("; pathway: ").Append(chosen.Id);
            text.Append(isMatched ? " (matched " + Describe(chosen.When) + ")" : " (no pathway matched, default)");
            return text.ToString();
        }

        private static string Describe(PathwayCondition condition)
        {
            if (condition.IsAlways())
            {
                return "always";
            }

            var text = new StringBuilder();
            AppendTags(text, "all of", condition.Required);
            AppendTags(text, "any of", condition.Any);
            return text.ToString();
        }

        // An undefined enum value (a hand-built pathway; the rules loader rejects them) has no name and is skipped.
        private static void AppendTags(StringBuilder text, string label, IReadOnlyList<ScenarioTag> tags)
        {
            var names = new List<string>(tags.Count);
            for (int i = 0; i < tags.Count; i++)
            {
                string name = NameOf(tags[i]);
                if (name != null)
                {
                    names.Add(name);
                }
            }

            if (names.Count == 0)
            {
                return;
            }

            text.Append(text.Length == 0 ? string.Empty : " and ").Append(label)
                .Append(" [").Append(string.Join(", ", names)).Append(']');
        }

        private static string NameOf(ScenarioTag tag)
        {
            int index = (int)tag;
            return index >= 0 && index < TagNames.Length ? TagNames[index] : null;
        }

        // snake_case, as in the rules file and the API: ForestRich -> forest_rich. Indexed by enum value.
        private static string[] BuildTagNames()
        {
            int size = 0;
            foreach (ScenarioTag tag in AllTags)
            {
                size = Math.Max(size, (int)tag + 1);
            }

            var names = new string[size];
            foreach (ScenarioTag tag in AllTags)
            {
                string pascal = tag.ToString();
                var snake = new StringBuilder(pascal.Length + 2);
                for (int i = 0; i < pascal.Length; i++)
                {
                    if (i > 0 && char.IsUpper(pascal[i]))
                    {
                        snake.Append('_');
                    }

                    snake.Append(char.ToLowerInvariant(pascal[i]));
                }

                names[(int)tag] = snake.ToString();
            }

            return names;
        }
    }
}
