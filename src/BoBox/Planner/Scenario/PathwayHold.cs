using System.Collections.Generic;

namespace BoBox.Planner.Scenario
{
    /// <summary>
    /// Hysteresis on one city's pathway, so a city near a threshold does not flip pathways every refresh.
    /// The held pathway stays until a different pathway id wins <see cref="WinsToSwitch"/> refreshes in a
    /// row and the first of those wins is at least a minimum of world seconds old, so refreshes that a
    /// zone-count change triggers in quick succession cannot flip it; a win for the held pathway, or for a
    /// third id, resets the challenger. Immutable and pure.
    /// A held id that the active rules no longer list gives way at once, so a rules reload never keeps a
    /// pathway that does not exist.
    /// </summary>
    public sealed class PathwayHold
    {
        public const int WinsToSwitch = 2;

        /// <summary>No pathway held yet: the first winner is adopted at once.</summary>
        public static readonly PathwayHold None = new PathwayHold(null, null, 0, 0);

        /// <summary>The pathway id in use, or null before the first refresh.</summary>
        public string HeldId { get; }

        /// <summary>The id that beat <see cref="HeldId"/> in the last refreshes, or null.</summary>
        public string ChallengerId { get; }

        /// <summary>Refreshes in a row the challenger has won. It can pass <see cref="WinsToSwitch"/> while the time gate holds.</summary>
        public int ChallengerWins { get; }

        /// <summary>World time of the challenger's first win in the current streak; meaningless without a challenger.</summary>
        public double ChallengerSince { get; }

        public PathwayHold(string heldId, string challengerId, int challengerWins, double challengerSince)
        {
            HeldId = heldId;
            ChallengerId = challengerId;
            ChallengerWins = challengerWins;
            ChallengerSince = challengerSince;
        }

        /// <summary>
        /// The hold after one refresh at world time <paramref name="now"/> whose classifier picked
        /// <paramref name="winnerId"/>. A challenger takes over once it has <see cref="WinsToSwitch"/> wins in a row
        /// and its first win is at least <paramref name="minSeconds"/> old.
        /// </summary>
        public PathwayHold Next(string winnerId, IReadOnlyList<Pathway> pathways, double now, double minSeconds)
        {
            if (HeldId == null || HeldId == winnerId || Find(HeldId, pathways) == null)
            {
                return new PathwayHold(winnerId, null, 0, 0);
            }

            // A clock that moved back (world time rewound) cannot extend a streak: start a new one.
            bool isStreak = winnerId == ChallengerId && now >= ChallengerSince;
            int wins = isStreak ? ChallengerWins + 1 : 1;
            double since = isStreak ? ChallengerSince : now;
            return wins >= WinsToSwitch && now - since >= minSeconds
                ? new PathwayHold(winnerId, null, 0, 0)
                : new PathwayHold(HeldId, winnerId, wins, since);
        }

        /// <summary>
        /// <paramref name="fresh"/> with the held pathway in place of the classifier's pick while a challenger
        /// has not won enough refreshes. Tags and scores stay the fresh ones.
        /// </summary>
        public ScenarioResult Apply(ScenarioResult fresh, IReadOnlyList<Pathway> pathways)
        {
            Pathway held = HeldId == fresh.Pathway.Id ? null : Find(HeldId, pathways);
            if (held == null)
            {
                return fresh;
            }

            string reason = "kept " + held.Id + " until " + fresh.Pathway.Id + " wins " + WinsToSwitch
                + " refreshes in a row over the minimum time (" + ChallengerWins + " so far); classifier: " + fresh.Reason;
            return new ScenarioResult(fresh.Tags, fresh.Scores, held, reason);
        }

        // The rules may lack an always-match pathway; the classifier then falls back to Pathway.Neutral, which counts as
        // listed only in that case. Otherwise a renamed default must give way at once like any other unlisted id.
        private static Pathway Find(string id, IReadOnlyList<Pathway> pathways)
        {
            if (id == null)
            {
                return null;
            }

            bool hasAlways = false;
            for (int i = 0; i < pathways.Count; i++)
            {
                if (pathways[i].Id == id)
                {
                    return pathways[i];
                }

                hasAlways |= pathways[i].When.IsAlways();
            }

            return id == Pathway.NeutralId && !hasAlways ? Pathway.Neutral : null;
        }
    }
}
