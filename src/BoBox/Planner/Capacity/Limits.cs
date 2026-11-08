using System;
using System.Collections.Generic;

namespace BoBox.Planner.Capacity
{
    /// <summary>
    /// Minimum and maximum counts per citizen job id and per building type id (type_*). Immutable.
    /// An id without an entry is unbounded, so the planners leave it alone.
    /// </summary>
    public sealed class Limits
    {
        public static readonly Limits Unbounded = new Limits();

        public IReadOnlyDictionary<string, CountRange> Jobs { get; }
        public IReadOnlyDictionary<string, CountRange> Buildings { get; }

        public Limits(
            IReadOnlyDictionary<string, CountRange> jobs = null,
            IReadOnlyDictionary<string, CountRange> buildings = null)
        {
            Jobs = MapCopy.Of(jobs);
            Buildings = MapCopy.Of(buildings);
        }

        public CountRange ForJob(string jobId)
        {
            return Jobs.TryGetValue(jobId, out CountRange range) ? range : CountRange.Unbounded;
        }

        public CountRange ForBuilding(string buildingType)
        {
            return Buildings.TryGetValue(buildingType, out CountRange range) ? range : CountRange.Unbounded;
        }

        public int ClampJob(string jobId, int count) => ForJob(jobId).Clamp(count);

        public int ClampBuilding(string buildingType, int count) => ForBuilding(buildingType).Clamp(count);
    }

    /// <summary>
    /// A count range with 0 &lt;= Min &lt;= Max, enforced on construction. The max is stored as its
    /// distance below int.MaxValue, so <c>default(CountRange)</c> is <see cref="Unbounded"/>, not 0..0.
    /// </summary>
    public readonly struct CountRange
    {
        public static readonly CountRange Unbounded = new CountRange(0, int.MaxValue);

        private readonly int _headroom;

        public int Min { get; }
        public int Max => int.MaxValue - _headroom;

        /// <summary>A negative min becomes 0; a max below min becomes min.</summary>
        public CountRange(int min, int max)
        {
            Min = Math.Max(0, min);
            _headroom = int.MaxValue - Math.Max(Min, max);
        }

        public bool IsBounded => Min > 0 || Max < int.MaxValue;

        public int Clamp(int count) => Math.Min(Max, Math.Max(Min, count));

        public bool IsBelowMin(int count) => count < Min;

        public bool IsAtMax(int count) => count >= Max;

        public override string ToString() => Min + ".." + (Max == int.MaxValue ? "inf" : Max.ToString());
    }
}
