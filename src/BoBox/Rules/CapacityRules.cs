using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BoBox.Rules
{
    /// <summary>
    /// Base capacity numbers, keyed by citizen job id (see Planner.JobIds) and by building type id
    /// (type_*, see Planner.Build.BuildingTypes). A key that is missing has no limit.
    /// </summary>
    public sealed class CapacityRules
    {
        private static readonly IReadOnlyDictionary<string, CapacityBase> None =
            new ReadOnlyDictionary<string, CapacityBase>(new Dictionary<string, CapacityBase>());

        public IReadOnlyDictionary<string, CapacityBase> Jobs { get; }
        public IReadOnlyDictionary<string, CapacityBase> Buildings { get; }

        public CapacityRules(
            IReadOnlyDictionary<string, CapacityBase> jobs = null,
            IReadOnlyDictionary<string, CapacityBase> buildings = null)
        {
            Jobs = Copy(jobs);
            Buildings = Copy(buildings);
        }

        private static IReadOnlyDictionary<string, CapacityBase> Copy(IReadOnlyDictionary<string, CapacityBase> source)
        {
            if (source == null)
            {
                return None;
            }

            var copy = new Dictionary<string, CapacityBase>();
            foreach (KeyValuePair<string, CapacityBase> pair in source)
            {
                if (pair.Value != null)
                {
                    copy[pair.Key] = pair.Value;
                }
            }

            return new ReadOnlyDictionary<string, CapacityBase>(copy);
        }
    }

    /// <summary>
    /// Inputs for one minimum and maximum. The minimum grows with population, the maximum with land, and
    /// its ceiling with population too: min = max(MinFloor, MinPerPop * population), max = min(ceiling,
    /// MaxPerZone * zones * map scale), ceiling = max(MaxCeiling * map scale, MaxPerPop * population).
    /// The capacity model owns the exact formula and keeps min &lt;= max.
    /// </summary>
    public sealed class CapacityBase
    {
        public const int NoCeiling = int.MaxValue;

        public double MinPerPop { get; }
        public double MaxPerZone { get; }
        public int MinFloor { get; }
        public int MaxCeiling { get; }

        /// <summary>Raises the ceiling to this many per person once the city outgrows <see cref="MaxCeiling"/>. 0 keeps the ceiling fixed.</summary>
        public double MaxPerPop { get; }

        public CapacityBase(
            double minPerPop = 0, double maxPerZone = 0, int minFloor = 0, int maxCeiling = NoCeiling, double maxPerPop = 0)
        {
            MinPerPop = minPerPop;
            MaxPerZone = maxPerZone;
            MinFloor = minFloor;
            MaxCeiling = maxCeiling;
            MaxPerPop = maxPerPop;
        }
    }
}
