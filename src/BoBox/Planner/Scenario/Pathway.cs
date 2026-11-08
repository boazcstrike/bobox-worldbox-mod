using System.Collections.Generic;
using System.Linq;

namespace BoBox.Planner.Scenario
{
    /// <summary>
    /// One entry of the rules file's ordered pathway list: when it applies, and how it shapes jobs,
    /// building priority and capacity. Immutable. Missing maps are empty, so a lookup falls back to neutral.
    /// </summary>
    public sealed class Pathway
    {
        public const string NeutralId = "default";
        public const double NeutralWeight = 1.0;
        public const int NeutralPriority = 0;

        /// <summary>Always matches, all weights and multipliers 1. Used when the rules have no default pathway.</summary>
        public static readonly Pathway Neutral = new Pathway(NeutralId);

        public string Id { get; }
        public PathwayCondition When { get; }

        /// <summary>Relative weight per citizen job id. 1.0 is neutral; a missing job is neutral.</summary>
        public IReadOnlyDictionary<string, double> JobWeights { get; }

        /// <summary>Priority per building type id (type_*). A missing type has priority 0 (<see cref="NeutralPriority"/>) and weight 1, and is still buildable; only a higher priority builds first.</summary>
        public IReadOnlyDictionary<string, int> BuildingPriority { get; }

        public CapacityMultipliers CapacityMultipliers { get; }

        public Pathway(
            string id,
            PathwayCondition when = null,
            IReadOnlyDictionary<string, double> jobWeights = null,
            IReadOnlyDictionary<string, int> buildingPriority = null,
            CapacityMultipliers capacityMultipliers = null)
        {
            Id = id ?? NeutralId;
            When = when ?? PathwayCondition.Always;
            JobWeights = MapCopy.Of(jobWeights);
            BuildingPriority = MapCopy.Of(buildingPriority);
            CapacityMultipliers = capacityMultipliers ?? CapacityMultipliers.None;
        }

        public double WeightForJob(string jobId)
        {
            return jobId != null && JobWeights.TryGetValue(jobId, out double weight) ? weight : NeutralWeight;
        }

        public int PriorityForBuilding(string buildingType)
        {
            return buildingType != null && BuildingPriority.TryGetValue(buildingType, out int priority) ? priority : NeutralPriority;
        }
    }

    /// <summary>
    /// Matches when the city has every <see cref="Required"/> tag and, if <see cref="Any"/> is not
    /// empty, at least one of those. Both empty means it always matches.
    /// </summary>
    public sealed class PathwayCondition
    {
        public static readonly PathwayCondition Always = new PathwayCondition();

        public IReadOnlyList<ScenarioTag> Required { get; }
        public IReadOnlyList<ScenarioTag> Any { get; }

        public PathwayCondition(IReadOnlyList<ScenarioTag> required = null, IReadOnlyList<ScenarioTag> any = null)
        {
            Required = (required ?? new ScenarioTag[0]).ToList().AsReadOnly();
            Any = (any ?? new ScenarioTag[0]).ToList().AsReadOnly();
        }

        public bool IsAlways() => Required.Count == 0 && Any.Count == 0;

        /// <summary>A null <paramref name="tags"/> counts as no tags.</summary>
        public bool Matches(IReadOnlyCollection<ScenarioTag> tags)
        {
            if (tags == null)
            {
                return IsAlways();
            }

            for (int i = 0; i < Required.Count; i++)
            {
                if (!Contains(tags, Required[i]))
                {
                    return false;
                }
            }

            if (Any.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < Any.Count; i++)
            {
                if (Contains(tags, Any[i]))
                {
                    return true;
                }
            }

            return false;
        }

        // Plain loop: Matches runs once per pathway per city every few seconds, so no LINQ delegates.
        private static bool Contains(IReadOnlyCollection<ScenarioTag> tags, ScenarioTag tag)
        {
            foreach (ScenarioTag candidate in tags)
            {
                if (candidate == tag)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Multiplies capacity maximums and minimums, keyed by job id and by building type id. Missing is 1.</summary>
    public sealed class CapacityMultipliers
    {
        public const double Neutral = 1.0;

        public static readonly CapacityMultipliers None = new CapacityMultipliers();

        public IReadOnlyDictionary<string, double> Jobs { get; }
        public IReadOnlyDictionary<string, double> Buildings { get; }

        public CapacityMultipliers(
            IReadOnlyDictionary<string, double> jobs = null,
            IReadOnlyDictionary<string, double> buildings = null)
        {
            Jobs = MapCopy.Of(jobs);
            Buildings = MapCopy.Of(buildings);
        }

        public double ForJob(string jobId)
        {
            return jobId != null && Jobs.TryGetValue(jobId, out double value) ? value : Neutral;
        }

        public double ForBuilding(string buildingType)
        {
            return buildingType != null && Buildings.TryGetValue(buildingType, out double value) ? value : Neutral;
        }
    }
}
