using System.Collections.Generic;
using System.Linq;

namespace BoBox.Rules
{
    /// <summary>
    /// What <see cref="RulesLoader"/> produced. <see cref="Rules"/> is null when the file could not be
    /// used; the caller then keeps its last good rules. Warnings never block loading.
    /// </summary>
    public sealed class RulesLoadResult
    {
        public const string DefaultsSource = "defaults";

        public ColonyRules Rules { get; }

        /// <summary>"defaults", or the file path the rules came from.</summary>
        public string Source { get; }

        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }

        public RulesLoadResult(ColonyRules rules, string source, IEnumerable<string> errors, IEnumerable<string> warnings)
        {
            Rules = rules;
            Source = source ?? DefaultsSource;
            Errors = (errors ?? Enumerable.Empty<string>()).ToList().AsReadOnly();
            Warnings = (warnings ?? Enumerable.Empty<string>()).ToList().AsReadOnly();
        }

        public bool HasRules => Rules != null;

        public static RulesLoadResult Defaults(IEnumerable<string> warnings) =>
            new RulesLoadResult(ColonyRules.Default, DefaultsSource, null, warnings);

        public static RulesLoadResult Failed(string source, IEnumerable<string> errors) =>
            new RulesLoadResult(null, source, errors, null);
    }
}
