using System;
using System.Collections.Generic;
using BoBox.Planner.Capacity;
using BoBox.Planner.Scenario;

namespace BoBox.Planner
{
    /// <summary>
    /// Decides how many job slots a city opens, in need order: warriors (kept from the game), one reserved
    /// builder while a building waits, food, the other builders, wood, stone and ore, then roads and cleaners
    /// (kept from the game), then capacity minimums, then surplus. Pure: no game or Unity types.
    /// Design: docs/colony-brain.md.
    /// </summary>
    /// <remarks>
    /// Pathway and limits (<see cref="Plan(CitySnapshot, Pathway, Limits)"/>):
    /// <list type="bullet">
    /// <item>Across groups (food, wood, stone): the fixed share is the base. Its fraction of the
    /// remaining adults is multiplied by the mean pathway weight of the group's open jobs (cap above 0),
    /// then capped at <c>WholeShare</c>, all remaining adults. So a mean weight of 2 doubles the share,
    /// 0.5 halves it, and once share x weight reaches 1 every larger weight gives the same whole share
    /// (starving food share 0.7: farmer 3 and farmer 10 both take everyone). Builders scale by the builder
    /// weight. A group with no open job gets nothing. Weight 1 everywhere leaves the fixed shares as they were.</item>
    /// <item>Inside a group, and for the surplus, slots split in proportion to each job's weight
    /// (<see cref="PlanDraft.Distribute"/>), counting slots the job already holds, so farmer 1.5 and bushes
    /// 0.8 share about 1.5 : 0.8 overall. A weight of 0 or less closes the job; an unlisted job weighs 1.</item>
    /// <item>Builders: while a building waits, 1 per <c>AdultsPerBuilder</c> adults up to <c>MaxBuilders</c>, times the
    /// builder weight, lowered to the capacity maximum, and never below 1, even at weight 0 or a maximum of 0:
    /// construction must always be able to finish. The first builder is reserved right after the warriors,
    /// before food, so food cannot take every adult; only when warriors already took every adult is there none.
    /// The capacity minimum does not raise builders.</item>
    /// <item>Jobs kept from the game (warriors, roads, cleaners) are only lowered to their maximum,
    /// never raised to a minimum.</item>
    /// <item>No job ever goes above its capacity maximum: the maximum lowers the job's cap, so slots it
    /// cuts stay free and flow to the next needs and the surplus, in need order.</item>
    /// <item>After the needs, food, wood and (when mining is allowed) stone jobs below their capacity minimum
    /// are raised to it, in need order, while adults remain, but never above the job's cap (the game's gate
    /// lowered to the maximum). Minimums never take slots from other jobs.</item>
    /// <item>Mining waits for food (while starving) or wood (while low) only if a job in that group still has
    /// room under its cap after the wood phase; a group the pathway, limits or game gates closed never blocks it.
    /// While starving, the surplus fills food jobs with room first. Once food and wood have no room left, the
    /// surplus opens stone even when mining waited, so adults do not idle beside open ores.</item>
    /// <item>Total slots never exceed adults: <see cref="PlanDraft"/> hands out at most that many. Adults left
    /// over once every open job is at its cap are reported as <see cref="JobPlan.Idle"/>.</item>
    /// </list>
    /// </remarks>
    public static class ColonyPlanner
    {
        public const int MinFoodTarget = 30;
        public const int FoodPerAdult = 5;
        public const double StarvingFoodShare = 0.7;
        public const double FedFoodShare = 0.25;
        public const int AdultsPerBuilder = 3;
        public const int MaxBuilders = 6;
        public const int MinBuildersWhilePending = 1;
        public const int WoodBuffer = 40;
        public const double WoodLowShare = 0.6;
        public const double WoodOkShare = 0.2;
        public const int StoneBuffer = 30;
        public const double StoneLowShare = 0.4;
        public const double StoneOkShare = 0.15;
        public const int MaxMinersAtMine = 5;
        public const int MaxHunters = 1;

        private const double WholeShare = 1.0;
        private static readonly string BuilderReason = "building waiting, 1 per " + AdultsPerBuilder + " adults";

        private static readonly string[] RestJobs = { JobIds.RoadBuilder, JobIds.Cleaner, JobIds.ManureCleaner };

        public static JobPlan Plan(CitySnapshot city)
        {
            return Plan(city, null, null);
        }

        /// <summary>
        /// Plans with the city's scenario pathway and capacity limits. A null pathway means
        /// <see cref="Pathway.Neutral"/>, null limits mean <see cref="Capacity.Limits.Unbounded"/>.
        /// </summary>
        public static JobPlan Plan(CitySnapshot city, Pathway pathway, Limits limits)
        {
            if (city == null || city.Adults <= 0)
            {
                return JobPlan.Empty;
            }

            var caps = new JobCaps(city, pathway, limits);
            var draft = new PlanDraft(city.Adults);
            int foodTarget = Math.Max(MinFoodTarget, city.Adults * FoodPerAdult);
            bool isStarving = city.Food < foodTarget;
            bool isWoodLow = city.Wood < WoodBuffer;

            AddKept(city, caps, draft, JobIds.Attacker, "warriors, kept from game");
            ReserveBuilder(city, draft);
            AddFood(city, caps, draft, isStarving, foodTarget);
            AddBuilders(city, caps, draft);
            AddWood(city, caps, draft, isWoodLow);
            bool canMine = CanMine(caps, draft, isStarving, isWoodLow);
            if (canMine)
            {
                AddStone(city, caps, draft);
            }

            foreach (string jobId in RestJobs)
            {
                AddKept(city, caps, draft, jobId, "kept from game");
            }

            RaiseToMinimums(caps, draft, canMine);
            AddSurplus(caps, draft, isStarving, isWoodLow, canMine);
            return draft.ToPlan(IdleReason(draft.Remaining, caps));
        }

        // Only the maximum applies: the game opens these jobs for work it has seen (an army, roads,
        // ruins, manure), so a capacity minimum must not invent work the game gave 0 slots for.
        private static void AddKept(CitySnapshot city, JobCaps caps, PlanDraft draft, string jobId, string reason)
        {
            draft.Add(jobId, Math.Min(caps.RangeFor(jobId).Max, city.BaseSlotsFor(jobId)), reason);
        }

        private static void AddFood(CitySnapshot city, JobCaps caps, PlanDraft draft, bool isStarving, int foodTarget)
        {
            double share = isStarving ? StarvingFoodShare : FedFoodShare;
            string reason = "food " + city.Food + "/" + foodTarget
                + (isStarving ? ": under target, most adults to food" : ": at target, upkeep");
            AddShare(caps, draft, caps.Food, share, reason);
        }

        // The floor of 1 builder is taken before food, so a starving colony cannot spend every adult on
        // food and leave a waiting building unbuilt. Warriors come first: if they take every adult, none is left.
        private static void ReserveBuilder(CitySnapshot city, PlanDraft draft)
        {
            if (city.PendingBuildings > 0)
            {
                draft.Add(JobIds.Builder, MinBuildersWhilePending, BuilderReason);
            }
        }

        private static void AddBuilders(CitySnapshot city, JobCaps caps, PlanDraft draft)
        {
            if (city.PendingBuildings <= 0)
            {
                return;
            }

            int builders = Math.Min(MaxBuilders, Math.Max(1, city.Adults / AdultsPerBuilder));
            int weighted = (int)Math.Round(builders * caps.WeightFor(JobIds.Builder), MidpointRounding.AwayFromZero);
            int capped = Math.Min(caps.RangeFor(JobIds.Builder).Max, weighted);
            int target = Math.Max(MinBuildersWhilePending, capped);
            draft.Add(JobIds.Builder, target - draft.CountFor(JobIds.Builder), BuilderReason);
        }

        private static void AddWood(CitySnapshot city, JobCaps caps, PlanDraft draft, bool isWoodLow)
        {
            double share = isWoodLow ? WoodLowShare : WoodOkShare;
            string reason = "wood " + city.Wood + "/" + WoodBuffer + (isWoodLow ? ": under buffer" : ": upkeep");
            AddShare(caps, draft, caps.Wood, share, reason);
        }

        private static void AddStone(CitySnapshot city, JobCaps caps, PlanDraft draft)
        {
            bool isStoneLow = city.Stone < StoneBuffer;
            double share = isStoneLow ? StoneLowShare : StoneOkShare;
            string reason = "food and wood ok, stone " + city.Stone + "/" + StoneBuffer;
            AddShare(caps, draft, caps.Stone, share, reason);
        }

        // A group with no open job gets nothing either way, so skipping it changes no plan.
        private static void AddShare(
            JobCaps caps, PlanDraft draft, IReadOnlyList<JobCap> group, double share, string reason)
        {
            double weight = JobCaps.GroupWeight(group);
            if (weight <= 0)
            {
                return;
            }

            draft.Distribute(Share(draft.Remaining, Math.Min(WholeShare, share * weight)), group, reason);
        }

        private static void RaiseToMinimums(JobCaps caps, PlanDraft draft, bool canMine)
        {
            RaiseGroup(caps, draft, caps.Food);
            RaiseGroup(caps, draft, caps.Wood);
            if (canMine)
            {
                RaiseGroup(caps, draft, caps.Stone);
            }
        }

        private static void RaiseGroup(JobCaps caps, PlanDraft draft, IReadOnlyList<JobCap> group)
        {
            foreach (JobCap cap in group)
            {
                int minimum = caps.RangeFor(cap.JobId).Min;
                int target = Math.Min(minimum, cap.Cap);
                draft.Add(cap.JobId, target - draft.CountFor(cap.JobId), "capacity minimum " + minimum + " for population");
            }
        }

        // While starving, food jobs with room come first, so open farmer slots are never left empty
        // while wood takes the leftover adults.
        private static void AddSurplus(JobCaps caps, PlanDraft draft, bool isStarving, bool isWoodLow, bool canMine)
        {
            const string Reason = "surplus adults";
            if (isStarving)
            {
                draft.Distribute(draft.Remaining, caps.Food, Reason);
            }

            if (isWoodLow)
            {
                draft.Distribute(draft.Remaining, caps.Wood, Reason);
            }

            draft.Distribute(draft.Remaining, caps.Food, Reason);
            draft.Distribute(draft.Remaining, caps.Wood, Reason);
            if (canMine || !draft.HasRoom(caps.Food) && !draft.HasRoom(caps.Wood))
            {
                draft.Distribute(draft.Remaining, caps.Stone, Reason);
            }
        }

        // Mining waits for food and wood only while a job in that group can still take a slot, judged from
        // the caps (game gate, capacity maximum and pathway weight), not from the raw resources. Otherwise a
        // city whose farmers, hunters or woodcutters are all full or closed would leave its adults idle.
        private static bool CanMine(JobCaps caps, PlanDraft draft, bool isStarving, bool isWoodLow)
        {
            bool waitsForFood = isStarving && draft.HasRoom(caps.Food);
            bool waitsForWood = isWoodLow && draft.HasRoom(caps.Wood);
            return !waitsForFood && !waitsForWood;
        }

        private static string IdleReason(int idle, JobCaps caps)
        {
            if (idle <= 0)
            {
                return null;
            }

            if (!caps.HasOpenJob)
            {
                return "capped, idle " + idle + ": no job is open (pathway weights, limits or missing buildings closed every job)";
            }

            return "capped, idle " + idle + ": every open job is at its cap";
        }

        // At least one slot while anyone is free, so a tiny colony still covers every need in order.
        private static int Share(int remaining, double fraction)
        {
            return remaining <= 0 ? 0 : Math.Max(1, (int)Math.Floor(remaining * fraction));
        }
    }
}
