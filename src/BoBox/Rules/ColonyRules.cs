using System;
using System.Collections.Generic;
using System.Linq;
using BoBox.Planner.Scenario;
using Newtonsoft.Json;

namespace BoBox.Rules
{
    /// <summary>
    /// The whole rules file, immutable. Pure: no game or Unity types. Every section is non-null; a
    /// section missing from the file falls back to its defaults. The JSON shape is snake_case
    /// (see <see cref="RulesJson"/>), and rules/bobox-rules.json mirrors <see cref="Default"/>.
    /// </summary>
    public sealed class ColonyRules
    {
        public const int CurrentVersion = 1;

        private static readonly IReadOnlyList<Pathway> NoPathways = Array.Empty<Pathway>();
        private static readonly IReadOnlyList<MapScaleStep> NoMapScale = Array.Empty<MapScaleStep>();

        /// <summary>The built-in rules, used when the file is missing.</summary>
        public static ColonyRules Default => DefaultRules.Instance;

        public int Version { get; }
        public SignalRules Signals { get; }
        public ScenarioThresholds Scenarios { get; }

        /// <summary>Ordered decision list: the first pathway whose condition matches the city's tags wins.</summary>
        public IReadOnlyList<Pathway> Pathways { get; }

        public CapacityRules Capacity { get; }

        /// <summary>Ascending by <see cref="MapScaleStep.MinSize"/>. The last step at or below the map size applies.</summary>
        public IReadOnlyList<MapScaleStep> MapScale { get; }

        public ColonyRules(
            int version = CurrentVersion,
            SignalRules signals = null,
            ScenarioThresholds scenarios = null,
            IReadOnlyList<Pathway> pathways = null,
            CapacityRules capacity = null,
            IReadOnlyList<MapScaleStep> mapScale = null)
        {
            Version = version;
            Signals = signals ?? new SignalRules();
            Scenarios = scenarios ?? new ScenarioThresholds();
            Pathways = pathways == null ? NoPathways : pathways.Where(p => p != null).ToList().AsReadOnly();
            Capacity = capacity ?? new CapacityRules();
            MapScale = mapScale == null
                ? NoMapScale
                : mapScale.Where(s => s != null).OrderBy(s => s.MinSize).ToList().AsReadOnly();
        }

        /// <summary>A copy with another pathway list; everything else is shared.</summary>
        public ColonyRules WithPathways(IReadOnlyList<Pathway> pathways)
        {
            return new ColonyRules(Version, Signals, Scenarios, pathways, Capacity, MapScale);
        }

        /// <summary>The last pathway with an empty condition, or <see cref="Pathway.Neutral"/> when there is none.</summary>
        [JsonIgnore]
        public Pathway DefaultPathway =>
            Pathways.LastOrDefault(p => p.When.IsAlways()) ?? Pathway.Neutral;

        /// <summary>Factor for a map size in 64-tile units (standard is 4, bobox_16 is 16). 1.0 when no step applies.</summary>
        public double ScaleForMapSize(int mapSize)
        {
            MapScaleStep step = MapScale.LastOrDefault(s => s.MinSize <= mapSize);
            return step == null ? 1.0 : step.Factor;
        }
    }

    /// <summary>One map-scale step: from <see cref="MinSize"/> upward, maximums are multiplied by <see cref="Factor"/>.</summary>
    public sealed class MapScaleStep
    {
        public int MinSize { get; }
        public double Factor { get; }

        public MapScaleStep(int minSize = 0, double factor = 1.0)
        {
            MinSize = minSize;
            Factor = factor;
        }
    }
}
