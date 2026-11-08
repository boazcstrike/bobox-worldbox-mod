namespace BoBox.Rules
{
    /// <summary>
    /// Decides which load result a reload adopts. A missing file loads the defaults, which is right at
    /// startup but wrong after rules came from a file: an editor that deletes and recreates the file, or
    /// a rename race, would silently replace the tuned rules. That case becomes a failed result, so the
    /// caller keeps the last good rules and reports the error.
    /// </summary>
    public static class RulesReloadPolicy
    {
        public const string MissingFileError = "rules file missing, kept last good";

        /// <param name="result">What <see cref="RulesLoader.Load"/> returned.</param>
        /// <param name="currentSource">Source of the rules in use: "defaults" or a file path.</param>
        public static RulesLoadResult Apply(RulesLoadResult result, string currentSource)
        {
            bool fellBackToDefaults = result.HasRules && result.Source == RulesLoadResult.DefaultsSource;
            bool hadFileRules = currentSource != null && currentSource != RulesLoadResult.DefaultsSource;
            return fellBackToDefaults && hadFileRules
                ? RulesLoadResult.Failed(currentSource, new[] { MissingFileError })
                : result;
        }
    }
}
