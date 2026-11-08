using System;
using System.IO;
using BoBox.Rules;
using UnityEngine;

namespace BoBox.Brain
{
    /// <summary>
    /// Holds the rules every city check uses. Keep-last-good: a reload that fails leaves the current
    /// rules in place and reports why. The file lives next to the mod, in StreamingAssets/mods.
    /// Load and reload run on the main thread; readers get one immutable <see cref="ColonyRules"/>.
    /// Rules, source and load time sit in one immutable snapshot that a reload swaps whole, so a reader
    /// on another thread never sees rules from one load and a source from another.
    /// </summary>
    internal static class ActiveRules
    {
        public const string FileName = "bobox-rules.json";

        private static readonly object Gate = new object();
        private static volatile Snapshot _state = new Snapshot(ColonyRules.Default, RulesLoadResult.DefaultsSource, null, null);

        public static ColonyRules Current => _state.Rules;

        /// <summary>"defaults", or the path of the file the current rules came from.</summary>
        public static string Source => _state.Source;

        /// <summary>When rules were last adopted by a successful load (UTC), or null before the first one. A missing file still counts: it adopts the defaults.</summary>
        public static DateTime? LoadedAtUtc => _state.LoadedAtUtc;

        /// <summary>The last load attempt, including its errors and warnings. Null before the first load.</summary>
        public static RulesLoadResult LastResult => _state.LastResult;

        /// <summary>Rules, source, load time and last result as one consistent read. Take it once per response.</summary>
        public static Snapshot Capture() => _state;

        public static string FilePath => Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), FileName);

        /// <summary>
        /// Loads the file, adopts its rules when usable, and logs the outcome. A good load marks every
        /// city due in <see cref="ScenarioCache"/>. A failed load keeps the previous rules, source and load
        /// time; only <see cref="LastResult"/> changes. A missing file after rules came from a file counts
        /// as a failed load (<see cref="RulesReloadPolicy"/>); the defaults are adopted only at startup.
        /// </summary>
        public static RulesLoadResult Reload()
        {
            string path = FilePath;
            RulesLoadResult loaded = RulesLoader.Load(path);
            RulesLoadResult result;
            string keptSource;
            lock (Gate)
            {
                Snapshot previous = _state;
                keptSource = previous.Source;
                result = RulesReloadPolicy.Apply(loaded, previous.Source);
                _state = result.HasRules
                    ? new Snapshot(result.Rules, result.Source, DateTime.UtcNow, result)
                    : new Snapshot(previous.Rules, previous.Source, previous.LoadedAtUtc, result);
            }

            // Covers the startup load and POST /rules/reload: every city refreshes with the adopted rules.
            if (result.HasRules)
            {
                ScenarioCache.MarkAllDue();
            }

            foreach (string warning in result.Warnings)
            {
                Log.Warn("rules: " + warning);
            }

            foreach (string error in result.Errors)
            {
                Log.Error("rules: " + error + " (keeping rules from " + keptSource + ")");
            }

            return result;
        }

        internal sealed class Snapshot
        {
            public ColonyRules Rules { get; }
            public string Source { get; }
            public DateTime? LoadedAtUtc { get; }
            public RulesLoadResult LastResult { get; }

            /// <summary>False only when the last load attempt failed and the previous rules were kept.</summary>
            public bool LastLoadOk => LastResult == null || LastResult.HasRules;

            public Snapshot(ColonyRules rules, string source, DateTime? loadedAtUtc, RulesLoadResult lastResult)
            {
                Rules = rules;
                Source = source;
                LoadedAtUtc = loadedAtUtc;
                LastResult = lastResult;
            }
        }
    }
}
