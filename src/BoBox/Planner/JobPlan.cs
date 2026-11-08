using System;
using System.Collections.Generic;
using System.Linq;

namespace BoBox.Planner
{
    /// <summary>One plan line: how many slots a job gets, and why.</summary>
    public sealed class JobLine
    {
        public string JobId { get; }
        public int Count { get; }
        public string Reason { get; }

        public JobLine(string jobId, int count, string reason)
        {
            JobId = jobId;
            Count = count;
            Reason = reason;
        }
    }

    /// <summary>Immutable result of <see cref="ColonyPlanner.Plan"/>. Lines are in need order.</summary>
    public sealed class JobPlan
    {
        public static readonly JobPlan Empty = new JobPlan(new JobLine[0]);

        public IReadOnlyList<JobLine> Lines { get; }
        public int TotalSlots { get; }

        /// <summary>Adults the plan leaves without a slot, because every open job is at its cap. 0 or more.</summary>
        public int Idle { get; }

        /// <summary>Why <see cref="Idle"/> adults have no slot ("capped, idle N: ..."), or empty when none is idle.</summary>
        public string IdleReason { get; }

        /// <summary>One-line form of the lines and the idle count, for logs and for comparing two plans.</summary>
        public string Summary { get; }

        public JobPlan(IEnumerable<JobLine> lines, int idle = 0, string idleReason = null)
        {
            Lines = lines.ToList().AsReadOnly();
            TotalSlots = Lines.Sum(line => line.Count);
            Idle = Math.Max(0, idle);
            IdleReason = Idle == 0 ? string.Empty : idleReason ?? "idle " + Idle;
            string slots = Lines.Count == 0
                ? "no slots"
                : string.Join(", ", Lines.Select(line => line.JobId + "=" + line.Count));
            Summary = Idle == 0 ? slots : slots + ", idle=" + Idle;
        }

        public int CountFor(string jobId)
        {
            return Lines.Where(line => line.JobId == jobId).Sum(line => line.Count);
        }
    }
}
