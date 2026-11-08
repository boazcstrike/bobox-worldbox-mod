namespace BoBox.Planner.Build
{
    /// <summary>The build planner's pick and why. <see cref="Candidate"/> is null when nothing should be built.</summary>
    public sealed class BuildChoice
    {
        public BuildCandidate Candidate { get; }
        public string Reason { get; }

        public BuildChoice(BuildCandidate candidate, string reason)
        {
            Candidate = candidate;
            Reason = reason ?? string.Empty;
        }

        public bool HasCandidate => Candidate != null;

        public static BuildChoice None(string reason) => new BuildChoice(null, reason);

        public override string ToString() => (HasCandidate ? Candidate.ToString() : "nothing") + ": " + Reason;
    }
}
