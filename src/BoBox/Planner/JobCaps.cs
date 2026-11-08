using System;
using System.Collections.Generic;
using System.Linq;
using BoBox.Planner.Capacity;
using BoBox.Planner.Scenario;

namespace BoBox.Planner
{
    /// <summary>
    /// Per-job slot caps and weights for one <see cref="ColonyPlanner"/> call. A cap is the game's own
    /// gate (no windmill, no farmers), lowered to the capacity maximum, and 0 for a job the pathway
    /// weighs at 0 or less. Each group lists its jobs heaviest weight first; equal weights keep need
    /// order, so the neutral pathway gives the planner's original order.
    /// </summary>
    internal sealed class JobCaps
    {
        private readonly Pathway _pathway;
        private readonly Limits _limits;

        public JobCaps(CitySnapshot city, Pathway pathway, Limits limits)
        {
            _pathway = pathway ?? Pathway.Neutral;
            _limits = limits ?? Limits.Unbounded;
            Food = Ordered(new[]
            {
                Cap(JobIds.Farmer, city.HasWindmill ? city.CropTiles : 0),
                Cap(JobIds.GathererBushes, city.HasStorage ? city.Bushes : 0),
                Cap(JobIds.GathererHerbs, city.HasStorage ? city.Plants : 0),
                Cap(JobIds.GathererHoney, city.HasStorage ? city.Hives : 0),
                Cap(JobIds.Hunter, city.HasStorage ? ColonyPlanner.MaxHunters : 0),
            });
            Wood = Ordered(new[] { Cap(JobIds.Woodcutter, city.HasStorage ? city.Trees : 0) });
            Stone = Ordered(new[]
            {
                Cap(JobIds.MinerDeposit, city.HasStorage ? city.Ores : 0),
                Cap(JobIds.Miner, city.HasMine ? ColonyPlanner.MaxMinersAtMine : 0),
            });
        }

        // Caps mirror the game's gates: farmers need a windmill, gatherers, hunters, woodcutters
        // and deposit miners need a finished storage building, mine workers need a mine.
        public IReadOnlyList<JobCap> Food { get; }
        public IReadOnlyList<JobCap> Wood { get; }
        public IReadOnlyList<JobCap> Stone { get; }

        /// <summary>
        /// The pathway weight, 0 or more. A NaN or +Infinity weight is a broken scenario file, so it
        /// counts as <see cref="Pathway.NeutralWeight"/> instead of flooding the job. -Infinity is below 0,
        /// like any negative weight, and closes the job.
        /// </summary>
        public double WeightFor(string jobId)
        {
            double weight = _pathway.WeightForJob(jobId);
            if (double.IsNaN(weight) || double.IsPositiveInfinity(weight))
            {
                return Pathway.NeutralWeight;
            }

            return Math.Max(0, weight);
        }

        public CountRange RangeFor(string jobId) => _limits.ForJob(jobId);

        /// <summary>True when any job in any group has a cap above 0.</summary>
        public bool HasOpenJob => HasOpen(Food) || HasOpen(Wood) || HasOpen(Stone);

        /// <summary>True when the group has a job with a cap above 0.</summary>
        public static bool HasOpen(IReadOnlyList<JobCap> group)
        {
            return group.Any(cap => cap.Cap > 0);
        }

        /// <summary>Mean weight of the group's open jobs (cap above 0), or 0 when none is open.</summary>
        public static double GroupWeight(IReadOnlyList<JobCap> group)
        {
            double sum = 0;
            int open = 0;
            for (int i = 0; i < group.Count; i++)
            {
                if (group[i].Cap > 0)
                {
                    sum += group[i].Weight;
                    open++;
                }
            }

            return open == 0 ? 0 : sum / open;
        }

        private JobCap Cap(string jobId, int gameCap)
        {
            double weight = WeightFor(jobId);
            int cap = weight <= 0 ? 0 : Math.Min(gameCap, _limits.ForJob(jobId).Max);
            return new JobCap(jobId, cap, weight);
        }

        // OrderByDescending is a stable sort, so equal weights keep the listed order.
        private static IReadOnlyList<JobCap> Ordered(IEnumerable<JobCap> caps)
        {
            return caps.OrderByDescending(cap => cap.Weight).ToList().AsReadOnly();
        }
    }

    /// <summary>One job's slot cap and pathway weight (finite, 0 or more) inside a need group.</summary>
    internal readonly struct JobCap
    {
        public string JobId { get; }
        public int Cap { get; }
        public double Weight { get; }

        public JobCap(string jobId, int cap, double weight)
        {
            JobId = jobId;
            Cap = cap;
            Weight = weight;
        }
    }
}
