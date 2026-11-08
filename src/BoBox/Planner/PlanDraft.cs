using System;
using System.Collections.Generic;
using System.Linq;

namespace BoBox.Planner
{
    /// <summary>
    /// Working state for one <see cref="ColonyPlanner.Plan"/> call. Never hands out more slots than
    /// adults. Lines for the same job merge, keeping first-seen order and every reason.
    /// </summary>
    internal sealed class PlanDraft
    {
        private readonly List<string> _order = new List<string>();
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        private readonly Dictionary<string, List<string>> _reasons = new Dictionary<string, List<string>>();

        public int Remaining { get; private set; }

        public PlanDraft(int adults)
        {
            Remaining = Math.Max(0, adults);
        }

        public int CountFor(string jobId)
        {
            return _counts.TryGetValue(jobId, out int count) ? count : 0;
        }

        /// <summary>True when a job in the group has a cap above 0 and holds fewer slots than that cap.</summary>
        public bool HasRoom(IReadOnlyList<JobCap> caps)
        {
            return caps.Any(cap => cap.Cap > 0 && CountFor(cap.JobId) < cap.Cap);
        }

        public void Add(string jobId, int count, string reason)
        {
            int granted = Math.Min(count, Remaining);
            if (granted <= 0)
            {
                return;
            }

            if (!_counts.ContainsKey(jobId))
            {
                _order.Add(jobId);
                _counts[jobId] = 0;
                _reasons[jobId] = new List<string>();
            }

            _counts[jobId] += granted;
            if (!_reasons[jobId].Contains(reason))
            {
                _reasons[jobId].Add(reason);
            }

            Remaining -= granted;
        }

        /// <summary>
        /// Hands out up to <paramref name="amount"/> slots one at a time, split in proportion to each
        /// job's weight among the jobs still under their cap.
        /// </summary>
        /// <remarks>
        /// Sainte-Lague highest averages: the next slot goes to the open job with the largest
        /// weight / (2 x slots the job already holds + 1); ties go to the earlier job. Slots from earlier
        /// calls count, so the surplus keeps the same weight ratio. With equal weights
        /// that is round-robin that favours the job holding fewer slots.
        /// </remarks>
        public void Distribute(int amount, IReadOnlyList<JobCap> caps, string reason)
        {
            int left = Math.Min(amount, Remaining);
            while (left > 0)
            {
                int next = NextJob(caps);
                if (next < 0)
                {
                    return;
                }

                Add(caps[next].JobId, 1, reason);
                left--;
            }
        }

        // Index of the open job with the highest Sainte-Lague quotient, or -1 when every job is full.
        private int NextJob(IReadOnlyList<JobCap> caps)
        {
            int best = -1;
            double bestQuotient = 0;
            for (int i = 0; i < caps.Count; i++)
            {
                if (caps[i].Weight <= 0 || CountFor(caps[i].JobId) >= caps[i].Cap)
                {
                    continue;
                }

                double quotient = caps[i].Weight / (2.0 * CountFor(caps[i].JobId) + 1);
                if (best < 0 || quotient > bestQuotient)
                {
                    best = i;
                    bestQuotient = quotient;
                }
            }

            return best;
        }

        /// <summary>The plan, with the adults still unassigned as <see cref="JobPlan.Idle"/> and <paramref name="idleReason"/>.</summary>
        public JobPlan ToPlan(string idleReason)
        {
            return new JobPlan(
                _order.Select(id => new JobLine(id, _counts[id], string.Join("; ", _reasons[id]))),
                Remaining,
                idleReason);
        }
    }
}
